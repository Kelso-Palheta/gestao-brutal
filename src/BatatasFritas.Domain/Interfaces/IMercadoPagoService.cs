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

public record PagamentoPixRequest(
    long PedidoId,
    decimal Valor,
    string Descricao,
    string EmailPagador,
    string NotificationUrl
);

public record PagamentoPixResponse(
    long PagamentoId,
    string QrCodeBase64,
    string QrCodeTexto,
    System.DateTime ExpiraEm
);

// FASE 7 — Checkout Transparente Cartão
public record PagamentoCartaoRequest(
    long PedidoId,
    decimal Valor,
    string Token,
    string PaymentMethodId,
    int Installments,
    string EmailPagador,
    string NotificationUrl
);

public record PagamentoCartaoResponse(
    long PagamentoId,
    string Status,       // "approved" | "pending" | "rejected"
    string StatusDetail
);

public interface IMercadoPagoService
{
    Task<PreferenciaMPResponse> CriarPreferenciaAsync(PreferenciaMPRequest request);
    Task<PagamentoPixResponse> CriarPagamentoPixAsync(PagamentoPixRequest request);
    Task<PagamentoCartaoResponse> CriarPagamentoCartaoAsync(PagamentoCartaoRequest request);
    Task<PagamentoMpStatus> ConsultarPagamentoAsync(long pagamentoId);
    // resourceId = data.id do body do webhook (NÃO o header x-request-id)
    Task<bool> ValidarAssinaturaWebhookAsync(string xSignature, string resourceId);
}
