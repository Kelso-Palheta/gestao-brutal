namespace BatatasFritas.Shared.Enums;

/// <summary>
/// Indica em qual momento o pagamento é coletado.
/// Online = pago agora via MP (Pix, Checkout Pro).
/// NaEntrega = pago quando o entregador chegar (Dinheiro, maquininha física).
/// </summary>
public enum MomentoPagamento
{
    Online     = 1,
    NaEntrega  = 2
}
