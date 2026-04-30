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
}
