using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using NHibernate;
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
    private readonly NHibernate.ISession _session;
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<PedidosHub> _hub;

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
        NHibernate.ISession session,
        IUnitOfWork uow,
        IHubContext<PedidosHub> hub)
    {
        _pedidoRepository   = pedidoRepository;
        _bairroRepository   = bairroRepository;
        _produtoRepository  = produtoRepository;
        _receitaRepository  = receitaRepository;
        _insumoRepository   = insumoRepository;
        _movRepository      = movRepository;
        _carteiraRepository = carteiraRepository;
        _infinitePayService = infinitePayService;
        _config             = config;
        _session            = session;
        _uow                = uow;
        _hub                = hub;
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
            CarteiraCashback? carteiraCashback = null;
            if (dto.ValorCashbackUsado > 0)
            {
                if (string.IsNullOrWhiteSpace(dto.TelefoneCliente))
                    throw new System.Exception("Telefone é obrigatório para usar cashback.");

                var telLimpo = new string(dto.TelefoneCliente.Where(char.IsDigit).ToArray());
                carteiraCashback = await _carteiraRepository.FindAsync(c => c.Telefone == telLimpo);

                if (carteiraCashback == null || carteiraCashback.SaldoAtual < dto.ValorCashbackUsado)
                    throw new System.Exception("Saldo de cashback insuficiente.");

                carteiraCashback.UsarSaldo(dto.ValorCashbackUsado, "Uso em novo pedido");
                await _carteiraRepository.UpdateAsync(carteiraCashback);
            }

            // NHibernate gera o ID aqui ao salvar no banco
            await _pedidoRepository.AddAsync(pedido);

            // Atualiza o PedidoReferenciaId na transação de saída (usa a instância já carregada)
            if (carteiraCashback != null)
            {
                var transacao = carteiraCashback.Transacoes.LastOrDefault();
                if (transacao != null)
                {
                    transacao.PedidoReferenciaId = pedido.Id;
                    await _carteiraRepository.UpdateAsync(carteiraCashback);
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
            await BaixarEstoque(dto.Itens, pedido.Id);

            await _uow.CommitAsync();

            // Notifica o KDS em tempo real via SignalR
            await _hub.Clients.All.SendAsync("NovoPedido", pedido.Id);

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
            TipoAtendimento    = pedido.TipoAtendimento,
            LinkPagamento      = pedido.LinkPagamento,
            TrocoPara          = pedido.TrocoPara,
            SubtotalItens      = pedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade),
            TaxaEntrega        = pedido.BairroEntrega?.TaxaEntrega ?? 0m,
            ValorCashbackUsado = pedido.ValorCashbackUsado,
            ValorTotal         = pedido.ValorTotal,
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
    private async Task BaixarEstoque(List<NovoItemPedidoDto> itens, int pedidoId)
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
                        $"Baixa automática — Pedido #{pedidoId}");

                    await _movRepository.AddAsync(mov);
                    await _insumoRepository.UpdateAsync(insumo);
                }
                else
                {
                    // Estoque insuficiente: ajusta assim mesmo (vai para negativo) e gera alerta visual
                    insumo.AjustarEstoque(-qtdConsumida);
                    await _insumoRepository.UpdateAsync(insumo);
                }
            }
        }
    }

    // ── DELETE api/pedidos/limpar-tudo ─────────────────────────────────
    [HttpDelete("limpar-tudo")]
    public async Task<IActionResult> LimparTudo()
    {
        try
        {
            _uow.BeginTransaction();
            await _session.CreateSQLQuery("DELETE FROM itens_pedido").ExecuteUpdateAsync();
            await _session.CreateSQLQuery("DELETE FROM pedidos").ExecuteUpdateAsync();
            await _uow.CommitAsync();
            return Ok(new { mensagem = "Todos os pedidos foram apagados com sucesso." });
        }
        catch (System.Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest($"Erro ao limpar pedidos: {ex.Message}");
        }
    }
}
