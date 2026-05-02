namespace BatatasFritas.Shared.Enums;

public enum StatusPagamento
{
    Pendente         = 1,
    Aprovado         = 2,
    Recusado         = 3,
    Cancelado        = 4,

    // Pagamento split: 1ª parte online aprovada, 2ª parte aguarda entrega
    PagamentoParcial = 5,

    // Para pagamentos feitos fisicamente em dinheiro ou outra forma manual
    Presencial       = 10,

    // Pagamento aprovado manualmente, mas pedido cancelado após aprovação (estorno físico pelo operador)
    Estornado        = 6
}
