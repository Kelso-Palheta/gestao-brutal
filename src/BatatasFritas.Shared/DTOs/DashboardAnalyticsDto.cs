using System;
using System.Collections.Generic;

namespace BatatasFritas.Shared.DTOs;

/// <summary>
/// DTO para o Dashboard de Analytics Financeiro com cards separados por categoria.
/// </summary>
public class DashboardAnalyticsDto
{
    // === CARD DOURADO: BATATAS ===
    public decimal ReceitaBatatas { get; set; }
    public int QuantidadeBatatasVendidas { get; set; }
    public decimal CashbackGeradoBatatas { get; set; }

    // === CARD AZUL: BEBIDAS ===
    public decimal ReceitaBebidas { get; set; }
    public int QuantidadeBebidasVendidas { get; set; }

    // === CARD VERDE: ENTREGAS ===
    public decimal ReceitaEntregas { get; set; }
    public int TotalEntregas { get; set; }
    public decimal TaxaEntregaMedia { get; set; }

    // === RESUMO GERAL ===
    public decimal ReceitaTotal => ReceitaBatatas + ReceitaBebidas + ReceitaEntregas;
    public int TotalPedidosFinalizados { get; set; }
    public decimal TicketMedio => TotalPedidosFinalizados > 0 ? ReceitaTotal / TotalPedidosFinalizados : 0m;

    // === PERÍODO ===
    public DateTime? DataInicio { get; set; }
    public DateTime? DataFim { get; set; }

    // === LISTA DE PEDIDOS FINALIZADOS ===
    public List<PedidoResumoDto> PedidosFinalizados { get; set; } = new();
}

/// <summary>
/// Resumo simplificado de um pedido para a lista do rodapé.
/// </summary>
public class PedidoResumoDto
{
    public int Id { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public DateTime DataHoraPedido { get; set; }
    public string Bairro { get; set; } = string.Empty;
    public decimal ValorBatatas { get; set; }
    public decimal ValorBebidas { get; set; }
    public decimal TaxaEntrega { get; set; }
    public decimal ValorTotal { get; set; }
    public int QtdItens { get; set; }
}