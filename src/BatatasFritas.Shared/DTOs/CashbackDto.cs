namespace BatatasFritas.Shared.DTOs;

public class SaldoCashbackDto
{
    public string Telefone { get; set; } = string.Empty;
    public string NomeCliente { get; set; } = string.Empty;
    public decimal SaldoAtual { get; set; }
}

public class CashbackConfigDto
{
    // A porcentagem que o cliente ganha de volta em cada compra (ex: 5 para 5%)
    public decimal Porcentagem { get; set; }
}
