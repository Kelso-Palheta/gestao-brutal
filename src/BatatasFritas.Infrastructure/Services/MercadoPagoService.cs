using System;
using System.Security.Cryptography;
using System.Text;
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

    public MercadoPagoService(
        IOptions<MercadoPagoOptions> options,
        ILogger<MercadoPagoService> logger)
    {
        _options = options.Value;
        _logger = logger;
        MercadoPagoConfig.AccessToken = _options.AccessToken;
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
}
