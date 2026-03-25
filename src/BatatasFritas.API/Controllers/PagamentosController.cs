using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PagamentosController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IUnitOfWork _uow;
    private readonly string _webhookSecret;

    public PagamentosController(IRepository<Pedido> pedidoRepository, IUnitOfWork uow, IConfiguration config)
    {
        _pedidoRepository = pedidoRepository;
        _uow              = uow;
        _webhookSecret    = config["InfinitePay:WebhookSecret"] ?? string.Empty;
    }

    /// <summary>
    /// Webhook chamado pela InfinitePay quando o pagamento muda de status.
    /// Verifica assinatura HMAC-SHA256 quando WebhookSecret estiver configurado.
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        // Lê o corpo bruto (necessário para verificar a assinatura antes de deserializar)
        Request.EnableBuffering();
        var rawBody = await new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true).ReadToEndAsync();
        Request.Body.Position = 0;

        // Verifica assinatura quando o secret estiver configurado
        if (!string.IsNullOrEmpty(_webhookSecret))
        {
            var signature = Request.Headers["X-Signature"].ToString();
            if (string.IsNullOrEmpty(signature) || !VerifyHmac(rawBody, signature, _webhookSecret))
            {
                Console.WriteLine("Webhook InfinitePay rejeitado: assinatura inválida.");
                return Unauthorized("Assinatura inválida.");
            }
        }

        InfinitePayWebhookPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<InfinitePayWebhookPayload>(rawBody,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch
        {
            return BadRequest("Payload inválido.");
        }

        if (payload == null || string.IsNullOrEmpty(payload.order_nsu))
            return BadRequest("Sem order_nsu");

        if (!int.TryParse(payload.order_nsu, out int pedidoId))
            return BadRequest("NSU não é numérico");

        Console.WriteLine($"Webhook InfinitePay: NSU={pedidoId}, Status={payload.status}");

        var pedido = await _pedidoRepository.GetByIdAsync(pedidoId);
        if (pedido == null) return NotFound("Pedido não encontrado");

        pedido.StatusPagamento = payload.status switch
        {
            "paid" or "approved"       => StatusPagamento.Aprovado,
            "declined" or "refused"    => StatusPagamento.Recusado,
            "canceled" or "cancelled"  => StatusPagamento.Cancelado,
            _                          => pedido.StatusPagamento
        };

        try
        {
            _uow.BeginTransaction();
            await _pedidoRepository.UpdateAsync(pedido);
            await _uow.CommitAsync();
            return Ok(); // InfinitePay para de tentar ao receber 200
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return StatusCode(500, ex.Message);
        }
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static bool VerifyHmac(string body, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash     = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return string.Equals(expected, signature.ToLowerInvariant(), StringComparison.Ordinal);
    }
}

public class InfinitePayWebhookPayload
{
    public string order_nsu { get; set; } = string.Empty;
    public string status    { get; set; } = string.Empty;
}
