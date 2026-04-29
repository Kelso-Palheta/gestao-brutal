using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Domain.Interfaces;
using BatatasFritas.Infrastructure.Options;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
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
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
    private readonly MercadoPagoOptions _mpOptions;
    private readonly NHibernate.ISession _session;
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<PedidosHub> _hub;
    private readonly IMercadoPagoService _mercadoPago;

    public PedidosController(
        IRepository<Pedido> pedidoRepository,
        IRepository<Bairro> bairroRepository,
        IRepository<Produto> produtoRepository,
        IRepository<ItemReceita> receitaRepository,
        IRepository<Insumo> insumoRepository,
        IRepository<MovimentacaoEstoque> movRepository,
        IRepository<CarteiraCashback> carteiraRepository,
        Microsoft.Extensions.Configuration.IConfiguration config,
        IOptions<MercadoPagoOptions> mpOptions,
        NHibernate.ISession session,
        IUnitOfWork uow,
        IHubContext<PedidosHub> hub,
        IMercadoPagoService mercadoPago)
    {
        _pedidoRepository   = pedidoRepository;
        _bairroRepository   = bairroRepository;
        _produtoRepository  = produtoRepository;
        _receitaRepository  = receitaRepository;
        _insumoRepository   = insumoRepository;
        _movRepository      = movRepository;
        _carteiraRepository = carteiraRepository;
        _config             = config;
        _mpOptions          = mpOptions.Value;
        _session            = session;
        _uow                = uow;
        _hub                = hub;
        _mercadoPago        = mercadoPago;
    }

    // ── GET api/pedidos/mp-public-key — expõe chave pública MP ao frontend ──
    [HttpGet("mp-public-key")]
    public IActionResult GetMpPublicKey()
        => Ok(new { PublicKey = _mpOptions.PublicKey });

    // ── POST api/pedidos — cria pedido + dispara baixa de estoque ──────────
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] NovoPedidoDto dto)
    {
        try
        {
            System.Console.WriteLine($"DTO RECEBIDO: Nome={dto.NomeCliente}, Telefone={dto.TelefoneCliente}, Endereco={dto.EnderecoEntrega}, BairroId={dto.BairroEntregaId}, Pag={dto.MetodoPagamento}, Troco={dto.TrocoPara}, QtdItens={dto.Itens?.Count}, Cashback={dto.ValorCashbackUsado}");

            // ── PRÉ-CHECK de estoque (ANTES da transação) ───────────────────────
            // Produtos com receitas (insumos) são validados pelos insumos — não pelo produto.estoque_atual.
            // Produtos SEM receita usam o estoque direto do produto.
            foreach (var item in dto.Itens)
            {
                var temReceita = Convert.ToInt64(await _session
                    .CreateSQLQuery("SELECT COUNT(*) FROM itens_receita WHERE produto_id = :id")
                    .SetParameter("id", item.ProdutoId)
                    .UniqueResultAsync()) > 0;

                if (!temReceita)
                {
                    var estoqueAtual = Convert.ToInt32(await _session
                        .CreateSQLQuery("SELECT estoque_atual FROM produtos WHERE id = :id")
                        .SetParameter("id", item.ProdutoId)
                        .UniqueResultAsync());

                    if (estoqueAtual < item.Quantidade)
                    {
                        var nomeProduto = await _session
                            .CreateSQLQuery("SELECT nome FROM produtos WHERE id = :id")
                            .SetParameter("id", item.ProdutoId)
                            .UniqueResultAsync<string>();
                        return BadRequest($"Estoque insuficiente para o produto {nomeProduto}. Disponível: {estoqueAtual}, Solicitado: {item.Quantidade}.");
                    }
                }
            }

            var bairro = await _bairroRepository.GetByIdAsync(dto.BairroEntregaId);
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

            // ── Baixa automática de estoque ──────────────────────────────
            await BaixarEstoque(dto.Itens, pedido.Id);

            await _uow.CommitAsync();

            // ── Pix: gera pagamento direto MP (QR + copia-e-cola) após commit ──
            if (dto.MetodoPagamento == MetodoPagamento.Pix)
            {
                try
                {
                    var notificationUrl = _config["MercadoPago:NotificationUrl"] ?? string.Empty;
                    var pixRequest = new PagamentoPixRequest(
                        PedidoId: pedido.Id,
                        Valor: pedido.ValorTotal,
                        Descricao: $"Pedido #{pedido.Id} - BatatasFritas",
                        EmailPagador: "cliente@batatasfritas.com.br",
                        NotificationUrl: notificationUrl
                    );
                    var pixResponse = await _mercadoPago.CriarPagamentoPixAsync(pixRequest);
                    pedido.SetPagamentoPix(pixResponse.QrCodeBase64, pixResponse.QrCodeTexto, pixResponse.PagamentoId);
                    _uow.BeginTransaction();
                    await _pedidoRepository.UpdateAsync(pedido);
                    await _uow.CommitAsync();
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[MP] Falha ao gerar Pix para pedido {pedido.Id}: {ex.Message}");
                }
            }

            // Notifica o KDS em tempo real via SignalR
            await _hub.Clients.All.SendAsync("NovoPedido", pedido.Id);

            // ── FASE 7: Checkout transparente cartão via token MP Bricks ──────────
            string? cartaoStatusPagamento = null;
            string? cartaoStatusDetalhe = null;
            if (dto.MetodoPagamento == MetodoPagamento.Cartao && !string.IsNullOrWhiteSpace(dto.CardToken))
            {
                try
                {
                    var notifUrl = _config["MercadoPago:NotificationUrl"] ?? string.Empty;
                    var cartaoReq = new PagamentoCartaoRequest(
                        PedidoId:       pedido.Id,
                        Valor:          pedido.ValorTotal,
                        Token:          dto.CardToken,
                        PaymentMethodId: dto.CardPaymentMethodId ?? string.Empty,
                        Installments:   dto.CardInstallments > 0 ? dto.CardInstallments : 1,
                        EmailPagador:   dto.CardEmailPagador ?? "cliente@batatasfritas.com.br",
                        NotificationUrl: notifUrl
                    );
                    var cartaoResp = await _mercadoPago.CriarPagamentoCartaoAsync(cartaoReq);

                    var statusPag = cartaoResp.Status == "approved"
                        ? StatusPagamento.Aprovado
                        : cartaoResp.Status == "rejected"
                            ? StatusPagamento.Recusado
                            : StatusPagamento.Pendente;

                    pedido.SetPagamentoCartao(cartaoResp.PagamentoId, statusPag);
                    _uow.BeginTransaction();
                    await _pedidoRepository.UpdateAsync(pedido);
                    await _uow.CommitAsync();

                    cartaoStatusPagamento = cartaoResp.Status;
                    cartaoStatusDetalhe   = cartaoResp.StatusDetail;

                    // Cartão aprovado online → imprime agora (sem esperar entrega)
                    if (statusPag == StatusPagamento.Aprovado)
                        await _hub.Clients.All.SendAsync("ImprimirPedido", pedido.Id);
                }
                catch (System.Exception ex)
                {
                    System.Console.WriteLine($"[MP] Falha pagamento cartão pedido {pedido.Id}: {ex.Message}");
                }
            }

            // ── FASE 6: imprimir se pagamento é na entrega (não precisa esperar webhook) ──
            // Pix/Online → PrintAgent aguarda sinal do webhook via ImprimirPedido
            // Cartão físico na entrega (sem token Bricks) → imprime agora
            var deveImprimirAgora = dto.MomentoPagamento == BatatasFritas.Shared.Enums.MomentoPagamento.NaEntrega
                                    && string.IsNullOrWhiteSpace(dto.CardToken); // Bricks já imprimiu acima se aprovado
            if (deveImprimirAgora)
                await _hub.Clients.All.SendAsync("ImprimirPedido", pedido.Id);

            return Ok(new
            {
                PedidoId              = pedido.Id,
                Status                = pedido.Status.ToString(),
                StatusPagamento       = pedido.StatusPagamento.ToString(),
                LinkPagamento         = pedido.LinkPagamento,
                QrCodeBase64          = pedido.QrCodeBase64,
                QrCodeTexto           = pedido.QrCodeTexto,
                MpPagamentoId         = pedido.MpPagamentoId,
                CartaoStatus          = cartaoStatusPagamento,
                CartaoStatusDetalhe   = cartaoStatusDetalhe
            });
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

            // Separa receitas deste produto específico
            var receitasDoProduto = receitas.Where(r => r.Produto.Id == item.ProdutoId).ToList();

            if (receitasDoProduto.Any())
            {
                // ── Produto com Receita: estoque controlado pelos Insumos ──────────
                // Não usa produto.EstoqueAtual — a baixa acontece nos insumos abaixo.
            }
            else
            {
                // ── Produto sem Receita: usa estoque direto do produto ─────────────
                await _session.RefreshAsync(produto);

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
                        // Estoque insuficiente: ajusta assim mesmo (vai para negativo) e gera alerta visual
                        insumo.AjustarEstoque(-qtdConsumida);
                        await _insumoRepository.UpdateAsync(insumo);
                    }
                }
        }
    }

    // ── POST api/pedidos/{id}/confirmar-pagamento-entrega ──────────────
    // Chamado pelo operador KDS quando recebe o pagamento em mãos (2ª parte ou único na entrega).
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

            // Notifica KDS + PrintAgent (caso seja pagamento NaEntrega puro — sem webhook anterior)
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

    // ── POST api/pedidos/{id}/iniciar-point ────────────────────────────
    // Cria payment intent no MP Point (maquininha física do totem).
    [HttpPost("{id}/iniciar-point")]
    public async Task<IActionResult> IniciarPagamentoPoint(int id)
    {
        try
        {
            var pedido = await _pedidoRepository.GetByIdAsync(id);
            if (pedido == null) return NotFound();

            var intent = await _mercadoPago.CriarIntentPointAsync(pedido.Id, pedido.ValorTotal);
            return Ok(new { intentId = intent.IntentId, deviceId = intent.DeviceId });
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    // ── GET api/pedidos/point-intent/{intentId}/status ─────────────────
    // Proxy de polling para o status do intent Point (usado pelo totem).
    [HttpGet("point-intent/{intentId}/status")]
    public async Task<IActionResult> StatusIntentPoint(string intentId)
    {
        try
        {
            var status = await _mercadoPago.ConsultarIntentPointAsync(intentId);
            return Ok(new
            {
                state        = status.State,
                pagamentoId  = status.PagamentoMpId,
                status       = status.Status,
                statusDetail = status.StatusDetail
            });
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
        }
    }

    // ── DELETE api/pedidos/point-intent/{intentId} ─────────────────────
    // Cancela intent Point (chamado quando o cliente abandona a tela do totem).
    [HttpDelete("point-intent/{intentId}")]
    public async Task<IActionResult> CancelarIntentPoint(string intentId)
    {
        try
        {
            await _mercadoPago.CancelarIntentPointAsync(intentId);
            return Ok();
        }
        catch (System.Exception ex)
        {
            return BadRequest(new { erro = ex.Message });
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
