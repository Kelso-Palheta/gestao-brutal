using BatatasFritas.Shared.Enums;

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

    // Divisão por Método de Pagamento (Mês)
    public decimal PixMes { get; set; }
    public decimal CartaoMes { get; set; }
    public decimal DinheiroMes { get; set; }

    // Despesas do Mês por Categoria
    public decimal DespesaFuncionarioMes { get; set; }
    public decimal DespesaEnergiaAguaMes { get; set; }
    public decimal DespesaImpostoMes { get; set; }
    public decimal DespesaOutrosMes { get; set; }

    // Métricas do Período Selecionado (Filtro)
    public decimal VendasPeriodo { get; set; }
    public decimal ComprasPeriodo { get; set; }
    public decimal DespesasPeriodo { get; set; }
    public decimal LucroPeriodo => VendasPeriodo - ComprasPeriodo - DespesasPeriodo;

    public decimal PixPeriodo { get; set; }
    public decimal CartaoPeriodo { get; set; }
    public decimal DinheiroPeriodo { get; set; }
    public int TotalPedidosPeriodo { get; set; }

    // Despesas do Período por Categoria
    public decimal DespesaFuncionarioPeriodo { get; set; }
    public decimal DespesaEnergiaAguaPeriodo { get; set; }
    public decimal DespesaImpostoPeriodo { get; set; }
    public decimal DespesaOutrosPeriodo { get; set; }

    public DateTime? DataInicioFiltro { get; set; }
    public DateTime? DataFimFiltro { get; set; }

    // Cashback do Período
    public decimal CashbackConcedidoPeriodo { get; set; }
    public decimal CashbackUsadoPeriodo { get; set; }

    // Pedidos por Status do Período
    public List<PedidoStatusResumoDto> PedidosPorStatus { get; set; } = new();
}

public class PedidoStatusResumoDto
{
    public StatusPedido Status { get; set; }
    public int Quantidade { get; set; }
}
