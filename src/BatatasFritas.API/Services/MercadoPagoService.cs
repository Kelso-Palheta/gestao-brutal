using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.DTOs.MercadoPago;
using BatatasFritas.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace BatatasFritas.API.Services;

public class MercadoPagoService : IMercadoPagoService
{
    private readonly HttpClient _http;
    private readonly ILogger<MercadoPagoService> _logger;
    private readonly MercadoPagoOptions _options;

    // Retry pipeline para métodos Point Smart 2 (HTTP direto via HttpClient)
    // Backoff exponencial: 1s → 2s → 4s com jitter — somente erros transitórios
    private readonly ResiliencePipeline _pointRetryPipeline;

    public MercadoPagoService(
        HttpClient http,
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoService> logger)
    {
        _http    = http;
        _logger  = logger;
        _options = options.Value;

        _http.BaseAddress = new Uri("https://api.mercadopago.com");

        if (!string.IsNullOrEmpty(_options.AccessToken))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.AccessToken);

        // Polly v8: ResiliencePipelineBuilder com retry exponencial (3 tentativas)
        _pointRetryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                UseJitter        = true,
                ShouldHandle     = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "[Polly] Point retry #{Attempt} após {Delay:g} — {Reason}",
                        args.AttemptNumber + 1,
                        args.RetryDelay,
                        args.Outcome.Exception?.Message ?? "erro desconhecido");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    // ── Inicia pagamento conforme método do pedido (SRP: lógica sai do controller) ────
    public async Task IniciarPagamentoAsync(Pedido pedido)
    {
        if (string.IsNullOrEmpty(_options.AccessToken))
        {
            _logger.LogWarning("MercadoPago AccessToken não configurado. Pedido #{Id} sem integração MP.", pedido.Id);
            return;
        }

        var descricao = $"Pedido #{pedido.Id} - Batata Palheta Brutal";
        var deviceId  = _options.DeviceId;

        switch (pedido.MetodoPagamento)
        {
            case MetodoPagamento.PixOnline:
                var pix = await CriarPixOnlineAsync(pedido.Id, pedido.ValorTotal, descricao);
                pedido.SetMercadoPagoPix(
                    pix.Id.ToString(),
                    pix.PointOfInteraction?.TransactionData?.QrCode ?? string.Empty,
                    pix.DateOfExpiration ?? DateTime.UtcNow.AddMinutes(30));
                break;

            case MetodoPagamento.PixPoint:
                var pixPoint = await CriarIntentPointAsync(pedido.Id, pedido.ValorTotal, "pix", deviceId);
                pedido.SetMercadoPagoPoint(pixPoint.Id);
                break;

            case MetodoPagamento.CartaoCredito:
                var intentCredito = await CriarIntentPointAsync(pedido.Id, pedido.ValorTotal, "credit_card", deviceId);
                pedido.SetMercadoPagoPoint(intentCredito.Id);
                break;

            case MetodoPagamento.CartaoDebito:
                var intentDebito = await CriarIntentPointAsync(pedido.Id, pedido.ValorTotal, "debit_card", deviceId);
                pedido.SetMercadoPagoPoint(intentDebito.Id);
                break;

            case MetodoPagamento.CheckoutPro:
                var checkout = await CriarCheckoutProAsync(pedido.Id, pedido.ValorTotal, descricao, pedido.NomeCliente);
                pedido.SetMercadoPagoCheckoutPro(checkout.Id, checkout.InitPoint);
                break;

            case MetodoPagamento.Dinheiro:
                pedido.MarcarPresencial();
                break;
        }
    }

    // ── PIX dinâmico (delivery) — usa SDK MP, sem Polly ──────────────────────
    public async Task<MpPaymentResultDto> CriarPixOnlineAsync(int pedidoId, decimal valor, string descricao)
    {
        var payload = new
        {
            transaction_amount = valor,
            description        = descricao,
            payment_method_id  = "pix",
            date_of_expiration = DateTime.UtcNow.AddMinutes(30).ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
            external_reference = pedidoId.ToString(),
            payer              = new { email = "cliente@batatapalhetabrutal.com" },
            notification_url   = _options.NotificationUrl
        };

        return await PostAsync<MpPaymentResultDto>("/v1/payments", payload, "PIX");
    }

    // ── Point Smart 2 — cria intent com retry Polly (backoff exponencial, 3x) ─
    public async Task<MpPointIntentResultDto> CriarIntentPointAsync(
        int pedidoId, decimal valor, string tipoCartao, string deviceId)
    {
        var payload = new
        {
            amount      = valor,
            description = $"Pedido #{pedidoId} - Batata Palheta Brutal",
            payment     = new
            {
                installments      = 1,
                type              = tipoCartao,
                installments_cost = "seller"
            },
            additional_info = new { external_reference = pedidoId.ToString() }
        };

        return await _pointRetryPipeline.ExecuteAsync(async ct =>
            await PostAsync<MpPointIntentResultDto>(
                $"/v1/point/integration-api/devices/{deviceId}/payment-intents",
                payload,
                "Point"));
    }

    // ── Checkout Pro (link de pagamento) — usa SDK MP, sem Polly ─────────────
    public async Task<MpPreferenceResultDto> CriarCheckoutProAsync(
        int pedidoId, decimal valor, string descricao, string nomeCliente)
    {
        var payload = new
        {
            items = new[]
            {
                new { title = descricao, quantity = 1, unit_price = valor, currency_id = "BRL" }
            },
            external_reference = pedidoId.ToString(),
            payer              = new { name = nomeCliente },
            notification_url   = _options.NotificationUrl,
            expiration_date_to = DateTime.UtcNow.AddHours(24).ToString("yyyy-MM-ddTHH:mm:sszzz"),
            expires            = true
        };

        return await PostAsync<MpPreferenceResultDto>("/checkout/preferences", payload, "CheckoutPro");
    }

    // ── Consulta status de pagamento — GET idempotente, sem Polly ────────────
    public async Task<string> ConsultarStatusPagamentoAsync(string externalPaymentId)
    {
        var response = await _http.GetAsync($"/v1/payments/{externalPaymentId}");
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("MP consulta status erro {Status}", response.StatusCode);
            return "error";
        }

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("status").GetString() ?? "unknown";
    }

    // ── Cancela intent na maquininha — com retry Polly ────────────────────────
    public async Task CancelarIntentPointAsync(string intentId, string deviceId)
    {
        await _pointRetryPipeline.ExecuteAsync(async ct =>
        {
            var response = await _http.DeleteAsync(
                $"/v1/point/integration-api/devices/{deviceId}/payment-intents/{intentId}");

            if (!response.IsSuccessStatusCode)
                _logger.LogWarning(
                    "MP cancelamento intent {IntentId} falhou: {Status}", intentId, response.StatusCode);
        });
    }

    // ── Validação HMAC do webhook ─────────────────────────────────────────────
    // MP spec: manifest = "id:<data.id>;request-id:<x-request-id>;ts:<ts>;"
    // x-signature header formato: "ts=<timestamp>,v1=<HMAC-SHA256-hex>"
    public bool ValidarAssinaturaWebhook(string dataId, string requestId, string signature, string secret)
    {
        try
        {
            var parts = signature.Split(',');
            if (parts.Length < 2) return false;

            string ts   = string.Empty;
            string hash = string.Empty;

            foreach (var part in parts)
            {
                var kv = part.Trim().Split('=', 2);
                if (kv.Length != 2) continue;
                if (kv[0] == "ts") ts   = kv[1];
                if (kv[0] == "v1") hash = kv[1];
            }

            if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(hash)) return false;

            var manifest = $"id:{dataId};request-id:{requestId};ts:{ts};";
            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var msgBytes = Encoding.UTF8.GetBytes(manifest);

            using var hmac  = new HMACSHA256(keyBytes);
            var computed    = hmac.ComputeHash(msgBytes);
            var computedHex = Convert.ToHexString(computed).ToLower();

            return computedHex == hash;
        }
        catch
        {
            return false;
        }
    }

    // ── Helper POST genérico ──────────────────────────────────────────────────
    private async Task<T> PostAsync<T>(string url, object payload, string contexto)
    {
        var json     = JsonSerializer.Serialize(payload);
        var content  = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync(url, content);
        var body     = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("MP {Contexto} erro {Status}: {Body}", contexto, response.StatusCode, body);
            throw new Exception($"Erro Mercado Pago {contexto}: {response.StatusCode}");
        }

        return JsonSerializer.Deserialize<T>(body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception($"Resposta MP {contexto} inválida.");
    }
}
