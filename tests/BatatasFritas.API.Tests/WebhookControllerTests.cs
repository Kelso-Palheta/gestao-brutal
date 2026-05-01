using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BatatasFritas.Domain.Interfaces;
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

    public WebhookControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockMp = factory.MockMercadoPago;
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
        // mas o pedido não é duplicado porque o match por MpPagamentoId retorna null
        // (nenhum pedido no banco com esse MpPagamentoId — comportamento idempotente)
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

        // Body que não é JSON válido — HMAC já validado com resourceId vazio
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
        // Arrange — mock rejeita string vazia (header ausente vira "")
        _mockMp.ValidarAssinaturaWebhookAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(Task.FromResult(false));

        var content = CriarWebhookPayload("77777");
        // Intencionalmente não adiciona header x-signature
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhook/mercadopago") { Content = content };

        // Act
        var response = await _client.SendAsync(request);

        // Assert — assinatura ausente/inválida → 401
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
