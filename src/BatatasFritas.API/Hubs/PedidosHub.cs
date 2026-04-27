using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace BatatasFritas.API.Hubs;

/// <summary>
/// Hub SignalR para notificações em tempo real do KDS.
/// O servidor envia mensagens; clientes só ouvem (sem métodos recebíveis).
/// </summary>
public class PedidosHub : Hub
{
    // Método chamado quando o cliente se conecta (opcional, para log)
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    // Cliente entra no grupo de um pedido específico (para receber updates de pagamento)
    public Task JoinPedidoGroup(int pedidoId)
        => Groups.AddToGroupAsync(Context.ConnectionId, $"pedido-{pedidoId}");

    public Task LeavePedidoGroup(int pedidoId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, $"pedido-{pedidoId}");

    // Eventos emitidos pelo servidor:
    //   "NovoPedido"        → int pedidoId (broadcast - KDS)
    //   "StatusAtualizado"  → int pedidoId, string novoStatus
    //   "PedidoCancelado"   → int pedidoId
    //   "PagamentoAprovado" → int pedidoId (grupo "pedido-{id}")
}
