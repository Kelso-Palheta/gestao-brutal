namespace BatatasFritas.Shared.Enums;

public enum StatusPagamento
{
    Pendente = 1,
    Aprovado = 2,
    Recusado = 3,
    Cancelado = 4,

    // Para pagamentos feitos fisicamente em dinheiro ou outra forma manual
    Presencial = 10 
}
