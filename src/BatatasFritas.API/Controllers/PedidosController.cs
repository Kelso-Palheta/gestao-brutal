using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Globalization;
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

            // ── PRÉ-CHECK de estoque (ANTES da transação) ────────────────────
            foreach (var item in dto.Itens)
            {
                var receitas = await _receitaRepository.FindManyAsync(ir => ir.Produto.Id == item.ProdutoId);
                if (!receitas.Any())
                {
                    var produto = await _produtoRepository.GetByIdAsync(item.ProdutoId);
                    if (produto == null || produto.EstoqueAtual < item.Quantidade)
                    {
                        var nomeProduto = produto?.Nome ?? $"produto id {item.ProdutoId}";
                        return BadRequest($"Estoque insuficiente para o produto {nomeProduto}. Disponível: {produto?.EstoqueAtual ?? 0}, Solicitado: {item.Quantidade}.");
                    }
                }
            }

            var bairro = await _bairroRepository.GetByIdAsync(dto.BairroEntregaId);
            if (bairro == null && dto.TipoAtendimento == TipoAtendimento.Delivery)
                return BadRequest("Bairro de entrega não encontrado.");

            var pedido = new Pedido(
                dto.NomeCliente, dto.TelefoneCliente, dto.EnderecoEntrega, bairro,
                dto.MetodoPagamento, dto.TrocoPara, dto.TipoAtendimento,
                dto.ValorCashbackUsado, dto.SegundoMetodoPagamento, dto.ValorSegundoPagamento,
                dto.MomentoPagamento, dto.SegundoMomentoPagamento);

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

            // Atualiza o PedidoReferenciaId na transação de saída
            if (carteiraCashback != null)
            {
                var transacao = carteiraCashback.Transacoes.LastOrDefault();
                if (transacao != null)
                {
                    transacao.PedidoReferenciaId = pedido.Id;
                    await _carteiraRepository.UpdateAsync(carteiraCashback);
                }
            }

            // ── Baixa automática de estoque ──────────────────────────────
            await BaixarEstoque(dto.Itens, pedido.Id);

            await _uow.CommitAsync();

            // Notifica o KDS em tempo real via SignalR
            await _hub.Clients.All.SendAsync("NovoPedido", pedido.Id);

            // ── imprimir se pagamento é na entrega ──
            var deveImprimirAgora = dto.MomentoPagamento == BatatasFritas.Shared.Enums.MomentoPagamento.NaEntrega
                                    && dto.TipoAtendimento != TipoAtendimento.Totem;
            if (deveImprimirAgora)
                await _hub.Clients.All.SendAsync("ImprimirPedido", pedido.Id);

            return Ok(new
            {
                PedidoId        = pedido.Id,
                Status          = pedido.Status.ToString(),
                StatusPagamento = pedido.StatusPagamento.ToString(),
                LinkPagamento   = pedido.LinkPagamento,
                QrCodeBase64    = pedido.QrCodeBase64,
                QrCodeTexto     = pedido.QrCodeTexto,
                MpPagamentoId   = pedido.MpPagamentoId
            });
        }
        catch (System.Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ── GET api/pedidos/bydate?start=yyyy-MM-dd&end=yyyy-MM-dd&page=1&pageSize=20 ──
    [HttpGet("bydate")]
    public async Task<IActionResult> GetByDate(
        [FromQuery] string? start    = null,
        [FromQuery] string? end      = null,
        [FromQuery] int     page     = 1,
        [FromQuery] int     pageSize = 20)
    {
        if (page < 1)     return BadRequest("page deve ser >= 1");
        if (pageSize < 1) return BadRequest("pageSize deve ser >= 1");

        var dataInicio = ParseData(start) ?? DateTime.UtcNow.Date.AddDays(-6);
        var dataFim    = (ParseData(end) ?? DateTime.UtcNow.Date).AddDays(1).AddTicks(-1);

        var paged = await _pedidoRepository.GetPagedAsync(
            p => p.DataHoraPedido >= dataInicio && p.DataHoraPedido <= dataFim,
            page, pageSize);

        var result = new PagedResult<ListaPedidosDto>
        {
            Items      = paged.Items.Select(p => new ListaPedidosDto
            {
                Id                 = p.Id,
                NomeCliente        = p.NomeCliente,
                TelefoneCliente    = p.TelefoneCliente,
                ValorTotal         = p.ValorTotal,
                DataHoraPedido     = p.DataHoraPedido,
                ValorCashbackUsado = p.ValorCashbackUsado
            }).ToList(),
            TotalCount = paged.TotalCount,
            Page       = paged.Page,
            PageSize   = paged.PageSize
        };

        return Ok(result);
    }

    private static DateTime? ParseData(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        return DateTime.TryParseExact(val, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
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
            SegundoMetodoPagamento = pedido.SegundoMetodoPagamento,
            ValorSegundoPagamento = pedido.ValorSegundoPagamento,
            StatusPagamento = pedido.StatusPagamento,
            TipoAtendimento    = pedido.TipoAtendimento,
            LinkPagamento      = pedido.LinkPagamento,
            TrocoPara          = pedido.TrocoPara,
            SubtotalItens      = pedido.Itens.Sum(i => i.PrecoUnitario * i.Quantidade),
            TaxaEntrega        = pedido.BairroEntrega?.TaxaEntrega ?? 0m,
            ValorCashbackUsado = pedido.ValorCashbackUsado,
            ValorTotal         = pedido.ValorTotal,
            MomentoPagamento = pedido.MomentoPagamento,
            SegundoMomentoPagamento = pedido.SegundoMomentoPagamento,
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

    // ── Baixa automática de estoque por receita e produtos finais ───────────
    private async Task BaixarEstoque(List<NovoItemPedidoDto> itens, int pedidoId)
    {
        if (itens == null || !itens.Any()) return;

        var receitas = (await _receitaRepository.GetAllAsync()).ToList();

        foreach (var item in itens)
        {
            var produto = await _produtoRepository.GetByIdAsync(item.ProdutoId);
            if (produto == null) continue;

            var receitasDoProduto = receitas.Where(r => r.Produto.Id == item.ProdutoId).ToList();

            if (receitasDoProduto.Any())
            {
                // ── Produto com Receita: estoque controlado pelos Insumos ──────────
            }
            else
            {
                // ── Produto sem Receita: usa estoque direto do produto ─────────────
                produto = await _produtoRepository.GetByIdAsync(item.ProdutoId) ?? produto;

                if (produto.EstoqueAtual >= item.Quantidade)
                {
                    produto.AjustarEstoque(-item.Quantidade);
                    await _produtoRepository.UpdateAsync(produto);

                    if (produto.EstoqueAtual <= 0)
                    {
                        produto.Desativar();
                        await _produtoRepository.UpdateAsync(produto);
                        await _hub.Clients.All.SendAsync("ProdutoDesativado", produto.Id);
                    }
                }
                else
                {
                    throw new System.Exception($"Estoque insuficiente para o produto {produto.Nome}. Disponível: {produto.EstoqueAtual}, Solicitado: {item.Quantidade}.");
                }
            }

            // ── Subtrai insumos da receita (se houver) ────────────────────────────
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
                    insumo.AjustarEstoque(-qtdConsumida);
                    await _insumoRepository.UpdateAsync(insumo);
                }
            }
        }
    }

    // ── POST api/pedidos/{id}/confirmar-pagamento-entrega ──────────────
    [HttpPost("{id}/confirmar-pagamento-entrega")]
    public async Task<IActionResult> ConfirmarPagamentoEntrega(int id)
    {
        try
        {
            var pedido = await _pedidoRepository.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            _uow.BeginTransaction();
            pedido.ConfirmarPagamentoEntrega();
            await _pedidoRepository.UpdateAsync(pedido);
            await _uow.CommitAsync();

            await _hub.Clients.All.SendAsync("StatusAtualizado", pedido.Id, pedido.StatusPagamento.ToString());
            await _hub.Clients.All.SendAsync("ImprimirPedido", pedido.Id);

            return Ok(new { mensagem = "Pagamento confirmado.", StatusPagamento = pedido.StatusPagamento.ToString() });
        }
        catch (System.Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ── DELETE api/pedidos/limpar-tudo ─────────────────────────────────
    [HttpDelete("limpar-tudo")]
    public async Task<IActionResult> LimparTudo()
    {
        try
        {
            _uow.BeginTransaction();
            await _uow.ExecuteRawAsync("DELETE FROM itens_pedido");
            await _uow.ExecuteRawAsync("DELETE FROM pedidos");
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
