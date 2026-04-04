using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
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
    private readonly IHubContext<PedidosHub> _hub;

    public PagamentosController(IRepository<Pedido> pedidoRepository, IUnitOfWork uow, IConfiguration config, IHubContext<PedidosHub> hub)
    {
        _pedidoRepository = pedidoRepository;
        _uow              = uow;
        _webhookSecret    = config["InfinitePay:WebhookSecret"] ?? string.Empty;
        _hub              = hub;
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

        // OBRIGATÓRIO: Verifica assinatura HMAC-SHA256 para evitar fraudes.
        // Se _webhookSecret não estiver configurado em produção, isso é um erro crítico de segurança.
        var signature = Request.Headers["X-InfinitePay-Signature"].ToString();
        
        if (string.IsNullOrEmpty(_webhookSecret) || _webhookSecret.Contains("CHANGE_ME"))
        {
            Console.WriteLine($"ERRO CRÍTICO: InfinitePay:WebhookSecret não está configurado corretamente (Valor: '{_webhookSecret}'). Webhook rejeitado por segurança.");
            return StatusCode(500, "Erro interno de configuração de segurança.");
        }

        if (string.IsNullOrEmpty(signature) || !VerifyHmac(rawBody, signature, _webhookSecret))
        {
            Console.WriteLine($"Webhook InfinitePay rejeitado: assinatura inválida ou ausente. Signature: {signature}");
            return Unauthorized("Assinatura inválida.");
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

            // Notifica o KDS e o Dashboard em tempo real via SignalR
            await _hub.Clients.All.SendAsync("StatusAtualizado", pedidoId, pedido.StatusPagamento.ToString());

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
        try
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            var expected = Convert.ToHexString(hash).ToLowerInvariant();

            // Comparação de tempo constante para evitar ataques de temporização
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant())
            );
        }
        catch
        {
            return false;
        }
    }
}

public class InfinitePayWebhookPayload
{
    public string order_nsu { get; set; } = string.Empty;
    public string status    { get; set; } = string.Empty;
}
