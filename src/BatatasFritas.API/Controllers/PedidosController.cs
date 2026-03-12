using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PedidosController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IRepository<Bairro> _bairroRepository;
    private readonly IRepository<Produto> _produtoRepository;
    private readonly IRepository<ItemReceita> _receitaRepository;
    private readonly IRepository<Insumo> _insumoRepository;
    private readonly IRepository<MovimentacaoEstoque> _movRepository;
    private readonly IRepository<CarteiraCashback> _carteiraRepository;
    private readonly BatatasFritas.API.Services.IInfinitePayService _infinitePayService;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly IUnitOfWork _uow;

    public PedidosController(
        IRepository<Pedido> pedidoRepository,
        IRepository<Bairro> bairroRepository,
        IRepository<Produto> produtoRepository,
        IRepository<ItemReceita> receitaRepository,
        IRepository<Insumo> insumoRepository,
        IRepository<MovimentacaoEstoque> movRepository,
        IRepository<CarteiraCashback> carteiraRepository,
        BatatasFritas.API.Services.IInfinitePayService infinitePayService,
        Microsoft.Extensions.Configuration.IConfiguration config,
        IUnitOfWork uow)
    {
        _pedidoRepository  = pedidoRepository;
        _bairroRepository  = bairroRepository;
        _produtoRepository = produtoRepository;
        _receitaRepository = receitaRepository;
        _insumoRepository  = insumoRepository;
        _movRepository     = movRepository;
        _carteiraRepository = carteiraRepository;
        _infinitePayService = infinitePayService;
        _config = config;
        _uow = uow;
    }

    // ── POST api/pedidos — cria pedido + dispara baixa de estoque ──────────
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] NovoPedidoDto dto)
    {
        try
        {
            System.Console.WriteLine($"DTO RECEBIDO: Nome={dto.NomeCliente}, Telefone={dto.TelefoneCliente}, Endereco={dto.EnderecoEntrega}, BairroId={dto.BairroEntregaId}, Pag={dto.MetodoPagamento}, Troco={dto.TrocoPara}, QtdItens={dto.Itens?.Count}, Cashback={dto.ValorCashbackUsado}");

            var bairro = await _bairroRepository.GetByIdAsync(dto.BairroEntregaId);
            var pedido = new Pedido(dto.NomeCliente, dto.TelefoneCliente, dto.EnderecoEntrega, bairro, dto.MetodoPagamento, dto.TrocoPara, dto.TipoAtendimento, dto.ValorCashbackUsado);

            foreach (var item in dto.Itens)
            {
                var produto = await _produtoRepository.GetByIdAsync(item.ProdutoId);
                if (produto != null)
                    pedido.AdicionarItem(produto, item.Quantidade, item.PrecoUnitario, item.Observacao);
            }

            _uow.BeginTransaction();

            // ── Verifica e desconta Cashback se solicitado ───────────────────
            if (dto.ValorCashbackUsado > 0)
            {
                if (string.IsNullOrWhiteSpace(dto.TelefoneCliente))
                    throw new System.Exception("Telefone é obrigatório para usar cashback.");

                var telLimpo = new string(dto.TelefoneCliente.Where(char.IsDigit).ToArray());
                var carteiras = await _carteiraRepository.GetAllAsync();
                var carteira = carteiras.FirstOrDefault(c => c.Telefone == telLimpo);

                if (carteira == null || carteira.SaldoAtual < dto.ValorCashbackUsado)
                    throw new System.Exception("Saldo de cashback insuficiente.");

                // A carteira cuida de deduzir o saldo e criar a TransacaoCashback de Saída (que é salva em cascade)
                carteira.UsarSaldo(dto.ValorCashbackUsado, "Uso em novo pedido");
                await _carteiraRepository.UpdateAsync(carteira);
            }

            // NHibernate gera o ID aqui ao salvar no banco
            await _pedidoRepository.AddAsync(pedido);

            // Atualiza a transação para referenciar o ID do pedido que acabou de ser gerado (se for o caso)
            if (dto.ValorCashbackUsado > 0)
            {
                var telLimpo = new string(dto.TelefoneCliente.Where(char.IsDigit).ToArray());
                var carteiras = await _carteiraRepository.GetAllAsync();
                var carteira = carteiras.FirstOrDefault(c => c.Telefone == telLimpo);
                var transacao = carteira?.Transacoes.LastOrDefault();
                if (transacao != null)
                {
                    transacao.PedidoReferenciaId = pedido.Id;
                    await _carteiraRepository.UpdateAsync(carteira!);
                }
            }

            // ── InfinitePay: Gerar Link para Pagamento Online ────────────────
            if (pedido.MetodoPagamento == MetodoPagamento.InfinitePayOnline)
            {
                var infiniteTag = _config["InfinitePay:InfiniteTag"];
                if (!string.IsNullOrEmpty(infiniteTag))
                {
                    var link = await _infinitePayService.GerarLinkPagamento(pedido, infiniteTag);
                    if (!string.IsNullOrEmpty(link))
                    {
                        pedido.SetLinkPagamento(link);
                        await _pedidoRepository.UpdateAsync(pedido);
                    }
                }
            }

            // ── Baixa automática de estoque ──────────────────────────────
            await BaixarEstoque(dto.Itens);

            await _uow.CommitAsync();

            return Ok(new { PedidoId = pedido.Id, Status = pedido.Status.ToString(), LinkPagamento = pedido.LinkPagamento });
        }
        catch (System.Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ── GET api/pedidos/{id} ──────────────────────────────────────────────
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var pedido = await _pedidoRepository.GetByIdAsync(id);
        if (pedido == null) return NotFound();

        var dto = new PedidoDetalheDto
        {
            Id              = pedido.Id,
            NomeCliente     = pedido.NomeCliente,
            TelefoneCliente = pedido.TelefoneCliente,
            EnderecoEntrega = pedido.EnderecoEntrega,
            NomeBairro      = pedido.BairroEntrega?.Nome ?? "Retirada",
            DataHoraPedido  = pedido.DataHoraPedido,
            Status          = pedido.Status,
            MetodoPagamento = pedido.MetodoPagamento,
            StatusPagamento = pedido.StatusPagamento,
            TipoAtendimento = pedido.TipoAtendimento,
            LinkPagamento   = pedido.LinkPagamento,
            TrocoPara       = pedido.TrocoPara,
            ValorTotal      = pedido.ValorTotal,
            Itens = pedido.Itens.Select(i => new ItemPedidoDetalheDto
            {
                Id          = i.Id,
                NomeProduto = i.Produto.Nome,
                Quantidade  = i.Quantidade,
                Observacao  = i.Observacao
            }).ToList()
        };

        return Ok(dto);
    }

    // ── Baixa automática de estoque por receita ───────────────────────────
    private async Task BaixarEstoque(List<NovoItemPedidoDto> itens)
    {
        if (itens == null || !itens.Any()) return;

        var receitas = (await _receitaRepository.GetAllAsync()).ToList();
        if (!receitas.Any()) return; // Nenhuma receita cadastrada — ignora silenciosamente

        foreach (var item in itens)
        {
            var receitasDoProduto = receitas.Where(r => r.Produto.Id == item.ProdutoId).ToList();
            foreach (var receita in receitasDoProduto)
            {
                var insumo = await _insumoRepository.GetByIdAsync(receita.Insumo.Id);
                if (insumo == null) continue;

                var qtdConsumida = receita.QuantidadePorUnidade * item.Quantidade;
                if (insumo.EstoqueAtual >= qtdConsumida)
                {
                    var mov = new MovimentacaoEstoque(
                        insumo,
                        TipoMovimentacao.Saida,
                        qtdConsumida,
                        insumo.CustoPorUnidade,
                        $"Baixa automática — Pedido #{item.ProdutoId}");

                    await _movRepository.AddAsync(mov);
                    await _insumoRepository.UpdateAsync(insumo);
                }
                // Se não houver estoque suficiente: registra a baixa sem travar o pedido
                // (estoque vai para negativo, gerando alerta visual)
                else
                {
                    insumo.AjustarEstoque(-qtdConsumida);
                    await _insumoRepository.UpdateAsync(insumo);
                }
            }
        }
    }
}
