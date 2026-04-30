using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using BatatasFritas.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MpOptions = BatatasFritas.Infrastructure.Options.MercadoPagoOptions;
using NSubstitute;
using Xunit;

namespace BatatasFritas.Infrastructure.Tests;

/// <summary>
/// Testa ValidarAssinaturaWebhookAsync — crítico para prevenir fraude de pagamento.
/// A assinatura MP usa HMAC-SHA256 com formato: "ts=TIMESTAMP,v1=HASH"
/// Manifest: "id:RESOURCE_ID;request-date:TIMESTAMP;"
/// </summary>
public class WebhookSignatureTests
{
    private const string SECRET  = "meu-webhook-secret-de-teste-32ch";
    private const string RESOURCE = "12345678";

    private readonly MercadoPagoService _svc;

    public WebhookSignatureTests()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new MpOptions
        {
            AccessToken   = "fake",
            WebhookSecret = SECRET,
            DeviceId      = "fake-device"
        });
        var httpFactory = Substitute.For<IHttpClientFactory>();
        _svc = new MercadoPagoService(options, NullLogger<MercadoPagoService>.Instance, httpFactory);
    }

    // ── Helper: gera assinatura válida ────────────────────────────────────────
    private static string GerarAssinatura(string resourceId, string ts, string secret)
    {
        var manifest  = $"id:{resourceId};request-date:{ts};";
        var keyBytes  = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(manifest);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(dataBytes);
        var v1   = BitConverter.ToString(hash).Replace("-", "").ToLower();
        return $"ts={ts},v1={v1}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Casos VÁLIDOS
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Signature_Valida_RetornaTrue()
    {
        var ts  = "1704067200";
        var sig = GerarAssinatura(RESOURCE, ts, SECRET);

        var result = await _svc.ValidarAssinaturaWebhookAsync(sig, RESOURCE);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Signature_Valida_PedidoIdDiferente_RetornaTrue()
    {
        var ts     = "9999999999";
        var resId  = "99887766";
        var sig    = GerarAssinatura(resId, ts, SECRET);

        var result = await _svc.ValidarAssinaturaWebhookAsync(sig, resId);

        result.Should().BeTrue();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Casos INVÁLIDOS — fraude / adulteração
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Signature_SecretErrado_RetornaFalse()
    {
        var ts  = "1704067200";
        var sig = GerarAssinatura(RESOURCE, ts, "secret-errado-completamente");

        var result = await _svc.ValidarAssinaturaWebhookAsync(sig, RESOURCE);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Signature_ResourceIdAdulterado_RetornaFalse()
    {
        var ts  = "1704067200";
        var sig = GerarAssinatura(RESOURCE, ts, SECRET);

        // Assinatura é válida para RESOURCE mas enviamos outro ID
        var result = await _svc.ValidarAssinaturaWebhookAsync(sig, "99999999");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Signature_HashAdulterado_RetornaFalse()
    {
        var ts  = "1704067200";
        var sig = $"ts={ts},v1=aabbccdd00112233aabbccdd00112233aabbccdd00112233aabbccdd00112233";

        var result = await _svc.ValidarAssinaturaWebhookAsync(sig, RESOURCE);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Signature_SemTs_RetornaFalse()
    {
        var result = await _svc.ValidarAssinaturaWebhookAsync("v1=aabbccdd", RESOURCE);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Signature_SemV1_RetornaFalse()
    {
        var result = await _svc.ValidarAssinaturaWebhookAsync("ts=1704067200", RESOURCE);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Signature_Vazia_RetornaFalse()
    {
        var result = await _svc.ValidarAssinaturaWebhookAsync("", RESOURCE);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task Signature_FormatoInvalido_RetornaFalse()
    {
        var result = await _svc.ValidarAssinaturaWebhookAsync("nao-e-um-header-valido", RESOURCE);

        result.Should().BeFalse();
    }
}
