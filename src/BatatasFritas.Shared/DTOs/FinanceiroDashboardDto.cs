namespace BatatasFritas.Shared.DTOs;

public class FinanceiroDashboardDto
{
    // Métricas do Dia Atual
    public decimal VendasHoje { get; set; }
    public decimal ComprasHoje { get; set; }
    public decimal LucroHoje => VendasHoje - ComprasHoje;
    
    // Métricas do Mês Atual
    public decimal VendasMes { get; set; }
    public decimal ComprasMes { get; set; }
    public decimal LucroMes => VendasMes - ComprasMes;

    // Métricas de Meta
    public decimal MetaDiaria { get; set; }
    public decimal ProgressoMeta => MetaDiaria > 0 ? (VendasHoje / MetaDiaria) * 100m : 0m;

    // Divisão por Método de Pagamento (Faturamento do Dia)
    public decimal PixHoje { get; set; }
    public decimal CartaoHoje { get; set; }
    public decimal DinheiroHoje { get; set; }
    public int TotalPedidosHoje { get; set; }
}
