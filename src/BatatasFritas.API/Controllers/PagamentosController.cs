using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PagamentosController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IUnitOfWork _uow;

    public PagamentosController(IRepository<Pedido> pedidoRepository, IUnitOfWork uow)
    {
        _pedidoRepository = pedidoRepository;
        _uow = uow;
    }

    /// <summary>
    /// Webhook chamado pela InfinitePay quando o pagamento muda de status
    /// </summary>
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] InfinitePayWebhookPayload payload)
    {
        Console.WriteLine($"Webhook InfinitePay recebido. NSU: {payload.order_nsu}, Status: {payload.status}");

        if (string.IsNullOrEmpty(payload.order_nsu))
            return BadRequest("Sem order_nsu");

        if (!int.TryParse(payload.order_nsu, out int pedidoId))
            return BadRequest("NSU não é numérico");

        var pedido = await _pedidoRepository.GetByIdAsync(pedidoId);
        if (pedido == null) return NotFound("Pedido não encontrado");

        // Atualiza o status de acordo com o webhook (InfinitePay envia "paid" quando aprovado)
        if (payload.status == "paid" || payload.status == "approved")
        {
            pedido.StatusPagamento = StatusPagamento.Aprovado;
        }
        else if (payload.status == "declined" || payload.status == "refused")
        {
            pedido.StatusPagamento = StatusPagamento.Recusado;
        }
        else if (payload.status == "canceled" || payload.status == "cancelled")
        {
            pedido.StatusPagamento = StatusPagamento.Cancelado;
        }

        _uow.BeginTransaction();
        await _pedidoRepository.UpdateAsync(pedido);
        await _uow.CommitAsync();

        return Ok(); // Responde 200 OK para a InfinitePay parar de tentar
    }
}

public class InfinitePayWebhookPayload
{
    public string order_nsu { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    // Outros campos da API da InfinitePay que não precisamos mapear totalmente
}
