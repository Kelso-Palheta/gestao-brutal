using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Shared.DTOs;

/// <summary>Snapshot de faturamento para o Dashboard do dono.</summary>
public class RelatorioDto
{
    // ---- Período selecionado pelo filtro ----
    public string DataInicioFiltro { get; set; } = string.Empty;
    public string DataFimFiltro    { get; set; } = string.Empty;

    // ---- Totais do período ----
    public int TotalPedidos         { get; set; }
    public decimal FaturamentoBruto { get; set; }
    public decimal TicketMedio      { get; set; }
    public decimal GastosCompras    { get; set; }
    public decimal LucroBruto       { get; set; }
    public decimal OutrasDespesas   { get; set; }
    public decimal LucroLiquido     { get; set; }

    // ---- Hoje (card fixo, sempre) ----
    public int PedidosHoje          { get; set; }
    public decimal FaturamentoHoje  { get; set; }

    // ---- Por Forma de Pagamento ----
    public List<PagamentoResumoDto> PorFormaPagamento { get; set; } = new();

    // ---- Produtos Mais Vendidos ----
    public List<ProdutoRankingDto> TopProdutos { get; set; } = new();

    // ---- Pedidos por Dia (do período, máx 31 dias no gráfico) ----
    public List<DiaResumoDto> PedidosPorDia { get; set; } = new();
}

public class PagamentoResumoDto
{
    public MetodoPagamento Metodo { get; set; }
    public int Quantidade { get; set; }
    public decimal Total { get; set; }
}

public class ProdutoRankingDto
{
    public string NomeProduto { get; set; } = string.Empty;
    public int QuantidadeVendida { get; set; }
    public decimal TotalGerado { get; set; }
}

public class DiaResumoDto
{
    public string Dia { get; set; } = string.Empty;
    public string DataCompleta { get; set; } = string.Empty;
    public int Pedidos { get; set; }
    public decimal Faturamento { get; set; }
}
