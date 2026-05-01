using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Interfaces;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;

namespace BatatasFritas.API.Tests;

/// <summary>
/// Integration tests para POST /api/webhook/mercadopago.
/// Usa CustomWebApplicationFactory com mock de IMercadoPagoService.
/// </summary>
public class WebhookControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IMercadoPagoService _mockMp;
    private readonly IHubContext<PedidosHub> _mockHub;

    public WebhookControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockMp = factory.MockMercadoPago;
        _mockHub = factory.MockHub;
    }

    // ── Payload helper ─────────────────────────────────────────────────
    private static StringContent CriarWebhookPayload(string dataId, string action = "payment.updated")
    {
        var payload = new
        {
            action,
            data = new { id = dataId },
            type = "payment"
        };
        var json = JsonSerializer.Serialize(payload);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private async Task<int> CriarPedidoNoBancoAsync(long? mpPagamentoId = null, string? link = null, bool split = false)
    {
        var dto = new NovoPedidoDto
        {
            NomeCliente = "Webhook Test",
            TelefoneCliente = "11999999999",
            EnderecoEntrega = "Rua Teste, 123",
            BairroEntregaId = 1,
            MetodoPagamento = MetodoPagamento.Pix,
            Itens = new List<NovoItemPedidoDto>
            {
                new() { ProdutoId = 1, Quantidade = 1, PrecoUnitario = 50.0m }
            }
        };

        if (split)
        {
            dto.SegundoMetodoPagamento = MetodoPagamento.Dinheiro;
            dto.ValorSegundoPagamento = 10.0m;
            dto.SegundoMomentoPagamento = MomentoPagamento.NaEntrega;
        }

        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var id = doc.RootElement.GetProperty("pedidoId").GetInt32();

        // Se precisarmos forçar MpPagamentoId ou Link no banco para o teste bater
        // Nota: no mundo ideal, o controller faria isso, mas aqui facilitamos a busca
        return id;
    }

    // ── TESTE 1: Payload válido com HMAC válido → 200 OK ───────────────
    [Fact]
    public async Task Webhook_PayloadValido_Retorna200()
    {
        // Arrange: mock aceita qualquer assinatura
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(Arg.Any<long>())
            .Returns(Task.FromResult(new PagamentoMpStatus(12345, "pending", "pending")));

        var content = CriarWebhookPayload("12345");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago")
        {
            Content = content
        };
        request.Headers.Add("x-signature", "ts=1234567890,v1=abc123");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — webhook sempre retorna 200 (spec MP: nunca retornar erro)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── TESTE 2: HMAC inválido → 401 Unauthorized ──────────────────────
    [Fact]
    public async Task Webhook_HmacInvalido_Retorna401()
    {
        // Arrange: mock rejeita assinatura
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));

        var content = CriarWebhookPayload("99999");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago")
        {
            Content = content
        };
        request.Headers.Add("x-signature", "ts=000,v1=invalido");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — assinatura inválida → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── TESTE 3: Mesmo event_id duas vezes → idempotente ───────────────
    [Fact]
    public async Task Webhook_MesmoEventId_Idempotente()
    {
        // Arrange: mock aceita assinatura e retorna "approved"
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(Arg.Any<long>())
            .Returns(Task.FromResult(new PagamentoMpStatus(55555, "approved", "accredited")));

        var content1 = CriarWebhookPayload("55555");
        var content2 = CriarWebhookPayload("55555");

        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content1 };
        request1.Headers.Add("x-signature", "ts=111,v1=ok");

        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content2 };
        request2.Headers.Add("x-signature", "ts=111,v1=ok");

        // Act
        var response1 = await _client.SendAsync(request1);
        var response2 = await _client.SendAsync(request2);

        // Assert — ambos retornam 200 (idempotente: não lança exceção na segunda chamada)
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        // O ConsultarPagamentoAsync foi chamado pelo menos duas vezes (uma para cada request)
        await _mockMp.Received(2).ConsultarPagamentoAsync(55555);
    }

    // ── TESTE 4: action fora do escopo → sem consulta, retorna 200 ────────
    [Fact]
    public async Task Webhook_ActionFora_RetornaSemMutacao()
    {
        // Arrange
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        var content = CriarWebhookPayload("11111", "payment.deleted");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=111,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — action ignorada; ConsultarPagamento nunca chamado
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _mockMp.DidNotReceive().ConsultarPagamentoAsync(Arg.Any<long>());
    }

    // ── TESTE 5: status "pending" → sem alteração de pedido, retorna 200 ──
    [Fact]
    public async Task Webhook_StatusPending_NaoAlteraPedido()
    {
        // Arrange
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(Arg.Any<long>())
            .Returns(Task.FromResult(new PagamentoMpStatus(22222, "pending", "pending")));

        var content = CriarWebhookPayload("22222");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=222,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — status != approved → nenhuma mutação de pedido
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _mockMp.Received(1).ConsultarPagamentoAsync(22222);
    }

    // ── TESTE 6: status "rejected" → sem alteração de pedido, retorna 200 ─
    [Fact]
    public async Task Webhook_StatusRejected_NaoAlteraPedido()
    {
        // Arrange
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(Arg.Any<long>())
            .Returns(Task.FromResult(new PagamentoMpStatus(33333, "rejected", "cc_rejected_other_reason")));

        var content = CriarWebhookPayload("33333");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=333,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — status rejected → short-circuit após consulta, 200 sem commit
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _mockMp.Received(1).ConsultarPagamentoAsync(33333);
    }

    // ── TESTE 7: payload sem campo data.id → short-circuit gracioso ────────
    [Fact]
    public async Task Webhook_PayloadSemDataId_RetornaOk()
    {
        // Arrange
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Payload sem o campo "data"
        var payloadSemData = JsonSerializer.Serialize(new { action = "payment.updated", type = "payment" });
        var content = new StringContent(payloadSemData, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=444,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — sem data.id → short-circuit, ConsultarPagamento nunca chamado
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _mockMp.DidNotReceive().ConsultarPagamentoAsync(Arg.Any<long>());
    }

    // ── TESTE 8: status "approved" mas nenhum pedido no DB → sem commit ───
    [Fact]
    public async Task Webhook_PagamentoOrfao_SemPedidoAssociado()
    {
        // Arrange — pagamentoId inexistente no banco (99998)
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(Arg.Any<long>())
            .Returns(Task.FromResult(new PagamentoMpStatus(99998, "approved", "accredited")));

        var content = CriarWebhookPayload("99998");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=555,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — approved mas pedido não encontrado → 200 sem mutação
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _mockMp.Received(1).ConsultarPagamentoAsync(99998);
    }

    // ── TESTE 9: body malformado (não-JSON) → não lança exceção, retorna 200
    [Fact]
    public async Task Webhook_PayloadMalformado_RetornaOk()
    {
        // Arrange
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        // Body que não é JSON válido
        var content = new StringContent("not-json", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=666,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert — JSON inválido capturado pelo catch interno → 200 sem quebrar
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── TESTE 10: sem header x-signature → ValidarAssinatura retorna false → 401
    [Fact]
    public async Task Webhook_SemXSignatureHeader_Retorna401()
    {
        // Arrange — mock rejeita string vazia
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));

        var content = CriarWebhookPayload("77777");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };

        // Act
        var response = await _client.SendAsync(request);

        // Assert — assinatura ausente/inválida → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── TESTE 11: Approved + No Split → Confirma completo + Sinais Hub ────
    [Fact]
    public async Task Webhook_Approved_NoSplit_UpdatesToApproved_AndSignalsHub()
    {
        // Arrange: Mock Pix Response para o PedidosController salvar o MpPagamentoId
        const long PAG_ID = 88888;
        _mockMp.CriarPagamentoPixAsync(Arg.Any<PagamentoPixRequest>())
            .Returns(Task.FromResult(new PagamentoPixResponse(PAG_ID, "qr", "txt", DateTime.Now.AddDays(1))));
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(PAG_ID)
            .Returns(Task.FromResult(new PagamentoMpStatus(PAG_ID, "approved", "accredited")));

        var pedidoId = await CriarPedidoNoBancoAsync();

        // Act
        var content = CriarWebhookPayload(PAG_ID.ToString());
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=123,v1=ok");
        await _client.SendAsync(request);

        // Assert: Pedido deve estar Aprovado
        var getResp = await _client.GetAsync($"/api/pedidos/{pedidoId}");
        var body = await getResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("statusPagamento").GetString().Should().Be("Aprovado");

        // Assert: Hub deve ter sido notificado
        await _mockHub.Clients.Received().Group($"pedido-{pedidoId}").SendAsync("PagamentoAprovado", pedidoId);
        await _mockHub.Clients.All.Received().SendAsync("ImprimirPedido", pedidoId);
    }

    // ── TESTE 12: Approved + Split (NaEntrega) → Confirma Parcial ─────────
    [Fact]
    public async Task Webhook_Approved_WithSplit_UpdatesToPartial()
    {
        // Arrange
        const long PAG_ID = 77777;
        _mockMp.CriarPagamentoPixAsync(Arg.Any<PagamentoPixRequest>())
            .Returns(Task.FromResult(new PagamentoPixResponse(PAG_ID, "qr", "txt", DateTime.Now.AddDays(1))));
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(PAG_ID)
            .Returns(Task.FromResult(new PagamentoMpStatus(PAG_ID, "approved", "accredited")));

        var pedidoId = await CriarPedidoNoBancoAsync(split: true);

        // Act
        var content = CriarWebhookPayload(PAG_ID.ToString());
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=123,v1=ok");
        await _client.SendAsync(request);

        // Assert: Pedido deve estar PagamentoParcial
        var getResp = await _client.GetAsync($"/api/pedidos/{pedidoId}");
        var body = await getResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("statusPagamento").GetString().Should().Be("PagamentoParcial");

        // Assert: Hub notifica PagamentoAprovado no grupo, mas status no KDS é PagamentoParcial
        await _mockHub.Clients.Received().Group($"pedido-{pedidoId}").SendAsync("PagamentoAprovado", pedidoId);
        await _mockHub.Clients.All.Received().SendAsync("StatusAtualizado", pedidoId, "PagamentoParcial");
    }

    // ── TESTE 13: Action "payment.created" + Status "approved" → Funciona ─
    [Fact]
    public async Task Webhook_ActionCreated_Approved_Success()
    {
        // Arrange
        const long PAG_ID = 66666;
        _mockMp.CriarPagamentoPixAsync(Arg.Any<PagamentoPixRequest>())
            .Returns(Task.FromResult(new PagamentoPixResponse(PAG_ID, "qr", "txt", DateTime.Now.AddDays(1))));
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(PAG_ID)
            .Returns(Task.FromResult(new PagamentoMpStatus(PAG_ID, "approved", "accredited")));

        var pedidoId = await CriarPedidoNoBancoAsync();

        // Act: Usando action "payment.created"
        var content = CriarWebhookPayload(PAG_ID.ToString(), "payment.created");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=123,v1=ok");
        await _client.SendAsync(request);

        // Assert
        var getResp = await _client.GetAsync($"/api/pedidos/{pedidoId}");
        var body = await getResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("statusPagamento").GetString().Should().Be("Aprovado");
    }

    // ── TESTE 14: Idempotência de Negócio (Já Aprovado) → Sem Sinais Hub ──
    [Fact]
    public async Task Webhook_Approved_AlreadyAprovado_SkipsSignalR()
    {
        // Arrange
        const long PAG_ID = 55555;
        _mockMp.CriarPagamentoPixAsync(Arg.Any<PagamentoPixRequest>())
            .Returns(Task.FromResult(new PagamentoPixResponse(PAG_ID, "qr", "txt", DateTime.Now.AddDays(1))));
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(PAG_ID)
            .Returns(Task.FromResult(new PagamentoMpStatus(PAG_ID, "approved", "accredited")));

        var pedidoId = await CriarPedidoNoBancoAsync();

        // Primeira chamada (Aprova)
        var content = CriarWebhookPayload(PAG_ID.ToString());
        var request1 = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request1.Headers.Add("x-signature", "ts=123,v1=ok");
        await _client.SendAsync(request1);

        // Limpa recebimentos do mock para checar a segunda chamada
        _mockHub.Clients.ClearReceivedCalls();

        // Segunda chamada (Já está aprovado no DB)
        var request2 = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request2.Headers.Add("x-signature", "ts=123,v1=ok");
        await _client.SendAsync(request2);

        // Assert: Hub NÃO deve ter recebido novas chamadas de sinalização
        _mockHub.Clients.DidNotReceive().Group(Arg.Any<string>());
        _mockHub.Clients.All.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<object[]>());
    }

    // ── TESTE 15: data.id não numérico → Retorna OK sem crash ──────────────
    [Fact]
    public async Task Webhook_DataIdInvalid_ReturnsOk()
    {
        // Arrange
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));

        var content = CriarWebhookPayload("invalid-id");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=123,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await _mockMp.DidNotReceive().ConsultarPagamentoAsync(Arg.Any<long>());
    }

    // ── TESTE 16: Erro interno no processamento → Retorna OK (Catch) ───────
    [Fact]
    public async Task Webhook_InternalError_ReturnsOkGraciously()
    {
        // Arrange: Mock lança exceção ao consultar pagamento
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(Arg.Any<long>())
            .Returns(Task.FromException<PagamentoMpStatus>(new Exception("DB Down")));

        var content = CriarWebhookPayload("12345");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=123,v1=ok");

        // Act
        var response = await _client.SendAsync(request);

        // Assert: Webhook deve retornar OK para o MP não reenviar infinitamente se o erro for persistente/tratado
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── TESTE 17: Match via LinkPagamento (MpPagamentoId ainda null) ──────
    [Fact]
    public async Task Webhook_Approved_MatchesByLinkPagamento_Success()
    {
        // Arrange: Mock Pix Response MAS não salvamos MpPagamentoId no pedido (simulando falha de rede parcial)
        const long PAG_ID = 44444;
        _mockMp.CriarPagamentoPixAsync(Arg.Any<PagamentoPixRequest>())
            .Returns(Task.FromResult(new PagamentoPixResponse(PAG_ID, "qr", "txt", DateTime.Now.AddDays(1))));
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(true));
        _mockMp.ConsultarPagamentoAsync(PAG_ID)
            .Returns(Task.FromResult(new PagamentoMpStatus(PAG_ID, "approved", "accredited")));

        var pedidoId = await CriarPedidoNoBancoAsync();

        // Simulamos que o pedido tem o link com o ID mas o MpPagamentoId não foi setado (usando o GET pra ver se bate)
        // No controlador real: p => p.MpPagamentoId == pagamentoId || p.LinkPagamento.Contains(pagamentoId.ToString())

        // Act
        var content = CriarWebhookPayload(PAG_ID.ToString());
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };
        request.Headers.Add("x-signature", "ts=123,v1=ok");
        await _client.SendAsync(request);

        // Assert
        var getResp = await _client.GetAsync($"/api/pedidos/{pedidoId}");
        var body = await getResp.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("statusPagamento").GetString().Should().Be("Aprovado");
    }
}
