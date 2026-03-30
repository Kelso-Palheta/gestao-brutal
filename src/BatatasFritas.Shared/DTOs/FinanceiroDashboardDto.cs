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

    // Métricas do Período Selecionado (Filtro)
    public decimal VendasPeriodo { get; set; }
    public decimal ComprasPeriodo { get; set; }
    public decimal DespesasPeriodo { get; set; }
    public decimal LucroPeriodo => VendasPeriodo - ComprasPeriodo - DespesasPeriodo;
    
    public decimal PixPeriodo { get; set; }
    public decimal CartaoPeriodo { get; set; }
    public decimal DinheiroPeriodo { get; set; }
    public int TotalPedidosPeriodo { get; set; }

    public DateTime? DataInicioFiltro { get; set; }
    public DateTime? DataFimFiltro { get; set; }
}
