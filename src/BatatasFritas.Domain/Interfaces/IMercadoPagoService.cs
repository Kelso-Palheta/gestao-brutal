using System.Threading.Tasks;

namespace BatatasFritas.Domain.Interfaces;

public record PreferenciaMPRequest(
    long PedidoId,
    decimal ValorTotal,
    string Descricao,
    string EmailCliente,
    string NotificationUrl
);

public record PreferenciaMPResponse(
    string IdPreferencia,
    string InitPointUrl,
    string QrCodeTexto
);

public record PagamentoMpStatus(
    long PagamentoId,
    string Status,
    string StatusDetail
);

public interface IMercadoPagoService
{
    Task<PreferenciaMPResponse> CriarPreferenciaAsync(PreferenciaMPRequest request);
    Task<PagamentoMpStatus> ConsultarPagamentoAsync(long pagamentoId);
    // resourceId = data.id do body do webhook (NÃO o header x-request-id)
    Task<bool> ValidarAssinaturaWebhookAsync(string xSignature, string resourceId);
}
