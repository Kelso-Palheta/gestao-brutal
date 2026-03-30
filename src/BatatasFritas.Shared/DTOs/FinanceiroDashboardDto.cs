namespace BatatasFritas.Shared.DTOs;

public class FinanceiroDashboardDto
{
    // Métricas do Dia Atual
    public decimal VendasHoje { get; set; }
    public decimal ComprasHoje { get; set; }
    public decimal DespesasHoje { get; set; }
    public decimal LucroHoje => VendasHoje - ComprasHoje - DespesasHoje;
    
    // Métricas do Mês Atual
    public decimal VendasMes { get; set; }
    public decimal ComprasMes { get; set; }
    public decimal DespesasMes { get; set; }
    public decimal LucroMes => VendasMes - ComprasMes - DespesasMes;

    // Métricas de Meta
    public decimal MetaDiaria { get; set; }
    public decimal ProgressoMeta => MetaDiaria > 0 ? (VendasHoje / MetaDiaria) * 100m : 0m;

    // Divisão por Método de Pagamento (Faturamento do Dia)
    public decimal PixHoje { get; set; }
    public decimal CartaoHoje { get; set; }
    public decimal DinheiroHoje { get; set; }
    public int TotalPedidosHoje { get; set; }
}
