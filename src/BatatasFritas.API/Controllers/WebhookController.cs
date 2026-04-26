using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Domain.Interfaces;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly IMercadoPagoService _mercadoPagoService;
    private readonly IRepository<Pedido> _pedidoRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        IMercadoPagoService mercadoPagoService,
        IRepository<Pedido> pedidoRepo,
        IUnitOfWork uow,
        ILogger<WebhookController> logger)
    {
        _mercadoPagoService = mercadoPagoService;
        _pedidoRepo = pedidoRepo;
        _uow = uow;
        _logger = logger;
    }

    [HttpPost("mercadopago")]
    public async Task<IActionResult> MercadoPago()
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync();

        var xSignature = Request.Headers["x-signature"].ToString();

        // Extrai data.id do body para usar como resourceId na validação HMAC
        string resourceId = string.Empty;
        try
        {
            using var docPre = JsonDocument.Parse(body);
            if (docPre.RootElement.TryGetProperty("data", out var dataElPre) &&
                dataElPre.TryGetProperty("id", out var idElPre))
            {
                resourceId = idElPre.GetString() ?? string.Empty;
            }
        }
        catch { }

        var assinaturaValida = await _mercadoPagoService.ValidarAssinaturaWebhookAsync(xSignature, resourceId);
        if (!assinaturaValida)
        {
            _logger.LogWarning("Webhook MP rejeitado — assinatura inválida. resourceId: {Id}", resourceId);
            return Unauthorized();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            if (!root.TryGetProperty("action", out var actionEl)) return Ok();
            var action = actionEl.GetString();

            if (action != "payment.updated" && action != "payment.created") return Ok();

            if (!root.TryGetProperty("data", out var dataEl)) return Ok();
            if (!dataEl.TryGetProperty("id", out var idEl)) return Ok();
            if (!long.TryParse(idEl.GetString(), out var pagamentoId)) return Ok();

            var status = await _mercadoPagoService.ConsultarPagamentoAsync(pagamentoId);

            if (status.Status == "approved")
            {
                var pedidos = await _pedidoRepo.GetAllAsync();
                var pedido = System.Linq.Enumerable.FirstOrDefault(pedidos,
                    p => p.LinkPagamento != null && p.LinkPagamento.Contains(pagamentoId.ToString()));

                if (pedido != null && pedido.StatusPagamento != StatusPagamento.Aprovado)
                {
                    _uow.BeginTransaction();
                    pedido.StatusPagamento = StatusPagamento.Aprovado;
                    await _uow.CommitAsync();
                    _logger.LogInformation("Pedido {PedidoId} aprovado via webhook MP", pedido.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar webhook MP — body: {Body}", body[..Math.Min(200, body.Length)]);
        }

        // Sempre retorna 200 — MP reenvia se receber != 2xx
        return Ok();
    }
}
