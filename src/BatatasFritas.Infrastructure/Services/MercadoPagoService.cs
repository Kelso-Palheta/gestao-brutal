using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BatatasFritas.Domain.Interfaces;
using BatatasFritas.Infrastructure.Options;
using MercadoPago.Client.Payment;
using MercadoPago.Client.Preference;
using MercadoPago.Config;
using MercadoPago.Resource.Preference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatatasFritas.Infrastructure.Services;

public class MercadoPagoService : IMercadoPagoService
{
    private readonly MercadoPagoOptions _options;
    private readonly ILogger<MercadoPagoService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    private const string PointBaseUrl = "https://api.mercadopago.com/point/integration-api";

    public MercadoPagoService(
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _options = options.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        MercadoPagoConfig.AccessToken = _options.AccessToken;
    }

    // ── Helpers para Point API ─────────────────────────────────────────────
    private HttpClient CreatePointClient()
    {
        var client = _httpClientFactory.CreateClient("MercadoPagoPoint");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.AccessToken);
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    public async Task<PreferenciaMPResponse> CriarPreferenciaAsync(PreferenciaMPRequest request)
    {
        var client = new PreferenceClient();

        var preferenceRequest = new PreferenceRequest
        {
            Items = new System.Collections.Generic.List<PreferenceItemRequest>
            {
                new PreferenceItemRequest
                {
                    Id = request.PedidoId.ToString(),
                    Title = request.Descricao,
                    Quantity = 1,
                    UnitPrice = request.ValorTotal,
                    CurrencyId = "BRL"
                }
            },
            Payer = new PreferencePayerRequest
            {
                Email = request.EmailCliente
            },
            NotificationUrl = request.NotificationUrl,
            BackUrls = new PreferenceBackUrlsRequest
            {
                Success = $"{_options.NotificationUrl}/sucesso",
                Failure = $"{_options.NotificationUrl}/falha",
                Pending = $"{_options.NotificationUrl}/pendente"
            },
            AutoReturn = "approved"
        };

        var preference = await client.CreateAsync(preferenceRequest);

        _logger.LogInformation("Preferência MP criada: {Id} para pedido {PedidoId}", preference.Id, request.PedidoId);

        return new PreferenciaMPResponse(
            IdPreferencia: preference.Id,
            InitPointUrl: preference.InitPoint,
            QrCodeTexto: string.Empty
        );
    }

    public async Task<PagamentoPixResponse> CriarPagamentoPixAsync(PagamentoPixRequest request)
    {
        var client = new PaymentClient();

        var paymentRequest = new PaymentCreateRequest
        {
            TransactionAmount = request.Valor,
            Description = request.Descricao,
            PaymentMethodId = "pix",
            ExternalReference = request.PedidoId.ToString(),
            NotificationUrl = string.IsNullOrWhiteSpace(request.NotificationUrl) ? null : request.NotificationUrl,
            Payer = new PaymentPayerRequest
            {
                Email = request.EmailPagador
            }
        };

        var payment = await client.CreateAsync(paymentRequest);

        var qrBase64 = payment.PointOfInteraction?.TransactionData?.QrCodeBase64 ?? string.Empty;
        var qrTexto = payment.PointOfInteraction?.TransactionData?.QrCode ?? string.Empty;
        var expira = payment.DateOfExpiration ?? System.DateTime.UtcNow.AddMinutes(30);

        _logger.LogInformation("Pagamento Pix MP criado: {Id} para pedido {PedidoId}", payment.Id, request.PedidoId);

        return new PagamentoPixResponse(
            PagamentoId: payment.Id ?? 0,
            QrCodeBase64: qrBase64,
            QrCodeTexto: qrTexto,
            ExpiraEm: expira
        );
    }

    // FASE 7 — Checkout transparente cartão via token MP Bricks
    public async Task<PagamentoCartaoResponse> CriarPagamentoCartaoAsync(PagamentoCartaoRequest request)
    {
        var client = new PaymentClient();

        var paymentRequest = new PaymentCreateRequest
        {
            TransactionAmount = request.Valor,
            Token             = request.Token,
            PaymentMethodId   = request.PaymentMethodId,
            Installments      = request.Installments,
            ExternalReference = request.PedidoId.ToString(),
            NotificationUrl   = string.IsNullOrWhiteSpace(request.NotificationUrl) ? null : request.NotificationUrl,
            Payer             = new PaymentPayerRequest { Email = request.EmailPagador }
        };

        var payment = await client.CreateAsync(paymentRequest);

        _logger.LogInformation(
            "Pagamento Cartão MP criado: {Id}, status={Status}, pedido={PedidoId}",
            payment.Id, payment.Status, request.PedidoId);

        return new PagamentoCartaoResponse(
            PagamentoId: payment.Id ?? 0,
            Status:      payment.Status ?? string.Empty,
            StatusDetail: payment.StatusDetail ?? string.Empty
        );
    }

    public async Task<PagamentoMpStatus> ConsultarPagamentoAsync(long pagamentoId)
    {
        var client = new PaymentClient();
        var payment = await client.GetAsync(pagamentoId);

        return new PagamentoMpStatus(
            PagamentoId: payment.Id ?? 0,
            Status: payment.Status ?? string.Empty,
            StatusDetail: payment.StatusDetail ?? string.Empty
        );
    }

    // resourceId = data.id extraído do body do webhook
    public Task<bool> ValidarAssinaturaWebhookAsync(string xSignature, string resourceId)
    {
        try
        {
            // Formato header: "ts=1704067200,v1=abc123hash"
            var parts = xSignature.Split(',');
            string ts = string.Empty;
            string v1 = string.Empty;

            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2)
                {
                    if (kv[0].Trim() == "ts") ts = kv[1].Trim();
                    if (kv[0].Trim() == "v1") v1 = kv[1].Trim();
                }
            }

            if (string.IsNullOrEmpty(ts) || string.IsNullOrEmpty(v1))
                return Task.FromResult(false);

            var manifest = $"id:{resourceId};request-date:{ts};";
            var keyBytes = Encoding.UTF8.GetBytes(_options.WebhookSecret);
            var dataBytes = Encoding.UTF8.GetBytes(manifest);

            using var hmac = new HMACSHA256(keyBytes);
            var hash = hmac.ComputeHash(dataBytes);
            var computed = BitConverter.ToString(hash).Replace("-", "").ToLower();
            var expected = Encoding.UTF8.GetBytes(v1);
            var actual = Encoding.UTF8.GetBytes(computed);

            return Task.FromResult(CryptographicOperations.FixedTimeEquals(actual, expected));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao validar assinatura webhook MP");
            return Task.FromResult(false);
        }
    }

    // ── FASE 8: MP Point Smart 2 — maquininha física (totem) ─────────────────

    public async Task<PointIntentResponse> CriarIntentPointAsync(long pedidoId, decimal valor)
    {
        if (string.IsNullOrWhiteSpace(_options.DeviceId))
            throw new InvalidOperationException("MercadoPago:DeviceId não configurado. Configure a variável MercadoPago__DeviceId.");

        var client = CreatePointClient();
        var amountCentavos = (int)(valor * 100);

        var body = new
        {
            amount = amountCentavos,
            additional_info = new
            {
                external_reference = pedidoId.ToString(),
                print_on_terminal  = true
            }
        };

        var url = $"{PointBaseUrl}/devices/{_options.DeviceId}/payment-intents";
        var response = await client.PostAsJsonAsync(url, body);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _logger.LogError("[Point] Falha ao criar intent para pedido {Id}: {Err}", pedidoId, err);
            throw new InvalidOperationException($"MP Point API retornou {(int)response.StatusCode}: {err}");
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var intentId = json.GetProperty("id").GetString() ?? throw new InvalidOperationException("MP Point: id ausente na resposta.");
        var deviceId = json.TryGetProperty("device_id", out var dev) ? (dev.GetString() ?? _options.DeviceId) : _options.DeviceId;

        _logger.LogInformation("[Point] Intent {IntentId} criado — pedido {PedidoId}, valor {Valor}", intentId, pedidoId, valor);

        return new PointIntentResponse(intentId, deviceId);
    }

    public async Task<PointIntentStatusResponse> ConsultarIntentPointAsync(string intentId)
    {
        var client = CreatePointClient();
        var url = $"{PointBaseUrl}/payment-intents/{intentId}";
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[Point] Falha ao consultar intent {IntentId}: {Err}", intentId, err);
            return new PointIntentStatusResponse("ERROR", null, null, err);
        }

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var state = json.TryGetProperty("state", out var s) ? (s.GetString() ?? "UNKNOWN") : "UNKNOWN";

        long?  pagamentoId  = null;
        string? status      = null;
        string? statusDetail = null;

        if (json.TryGetProperty("payment", out var paymentNode) && paymentNode.ValueKind == JsonValueKind.Object)
        {
            if (paymentNode.TryGetProperty("id", out var pid))
                pagamentoId = pid.ValueKind == JsonValueKind.Number ? pid.GetInt64() : null;
            if (paymentNode.TryGetProperty("status", out var pst))
                status = pst.GetString();
            if (paymentNode.TryGetProperty("status_detail", out var psd))
                statusDetail = psd.GetString();
        }

        return new PointIntentStatusResponse(state, pagamentoId, status, statusDetail);
    }

    public async Task CancelarIntentPointAsync(string intentId)
    {
        if (string.IsNullOrWhiteSpace(_options.DeviceId)) return;

        try
        {
            var client = CreatePointClient();
            var url = $"{PointBaseUrl}/devices/{_options.DeviceId}/payment-intents/{intentId}";
            var resp = await client.DeleteAsync(url);
            _logger.LogInformation("[Point] Cancelamento intent {IntentId}: {StatusCode}", intentId, (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Point] Erro ao cancelar intent {IntentId}", intentId);
        }
    }
}
