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

    // Eventos emitidos pelo servidor para todos os clientes:
    //   "NovoPedido"       → int pedidoId
    //   "StatusAtualizado" → int pedidoId, string novoStatus
    //   "PedidoCancelado"  → int pedidoId
}
