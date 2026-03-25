using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RelatoriosController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IRepository<MovimentacaoEstoque> _movRepository;
    private readonly IRepository<Despesa> _despRepository;

    public RelatoriosController(IRepository<Pedido> pedidoRepository, IRepository<MovimentacaoEstoque> movRepository, IRepository<Despesa> despRepository)
    {
        _pedidoRepository = pedidoRepository;
        _movRepository = movRepository;
        _despRepository = despRepository;
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET api/relatorios/dashboard?de=2025-02-01&ate=2025-02-28
    // Parâmetros opcionais. Sem parâmetros = últimos 7 dias (padrão anterior).
    // ─────────────────────────────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string? de  = null,
        [FromQuery] string? ate = null)
    {
        var todos = (await _pedidoRepository.GetAllAsync()).ToList();
        var entregues = todos.Where(p => p.Status == StatusPedido.Entregue).ToList();

        var agora       = DateTime.UtcNow;
        var inicioHoje  = agora.Date;

        // ── Período selecionado ───────────────────────────────────────────
        var dataInicio = ParseData(de)  ?? agora.Date.AddDays(-6);
        var dataFim    = ParseData(ate) ?? agora.Date;
        // Garante que dataFim inclua o dia inteiro
        var dataFimFinal = dataFim.AddDays(1).AddTicks(-1);

        var entreguesPeriodo = entregues
            .Where(p => p.DataHoraPedido >= dataInicio && p.DataHoraPedido <= dataFimFinal)
            .ToList();

        // ── Totais de Estoque (Gastos e Lucro) ────────────────────────────
        var movimentacoes = await _movRepository.GetAllAsync();
        var entradas = movimentacoes
            .Where(m => m.Tipo == TipoMovimentacao.Entrada && m.DataMovimentacao >= dataInicio && m.DataMovimentacao <= dataFimFinal)
            .ToList();

        var gastosCompras = entradas.Sum(e => e.ValorTotal);

        var despesasFull = await _despRepository.GetAllAsync();
        var despesasPeriodo = despesasFull
            .Where(d => d.DataRegistro >= dataInicio && d.DataRegistro <= dataFimFinal)
            .ToList();
        
        var outrasDespesas = despesasPeriodo.Sum(d => d.Valor);

        // ── Totais do período selecionado ─────────────────────────────────
        var faturamentoPeriodo = entreguesPeriodo.Sum(p => Valor(p));
        var ticketMedio        = entreguesPeriodo.Count > 0 ? faturamentoPeriodo / entreguesPeriodo.Count : 0m;
        var lucroBruto         = faturamentoPeriodo - gastosCompras;
        var lucroLiquido       = lucroBruto - outrasDespesas;

        // ── Hoje (sempre fixo para o card de topo) ─────────────────────────
        var entreguesHoje     = entregues.Where(p => p.DataHoraPedido.Date == inicioHoje).ToList();
        var faturamentoHoje   = entreguesHoje.Sum(p => Valor(p));

        // ── Por Forma de Pagamento (do período) ───────────────────────────
        var porPagamento = entreguesPeriodo
            .GroupBy(p => p.MetodoPagamento)
            .Select(g => new PagamentoResumoDto
            {
                Metodo    = g.Key,
                Quantidade = g.Count(),
                Total     = g.Sum(p => Valor(p))
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        // ── Top Produtos (do período) ─────────────────────────────────────
        var topProdutos = entreguesPeriodo
            .SelectMany(p => p.Itens)
            .GroupBy(i => i.Produto.Nome)
            .Select(g => new ProdutoRankingDto
            {
                NomeProduto      = g.Key,
                QuantidadeVendida = g.Sum(i => i.Quantidade),
                TotalGerado       = g.Sum(i => i.PrecoUnitario * i.Quantidade)
            })
            .OrderByDescending(x => x.QuantidadeVendida)
            .Take(8)
            .ToList();

        // ── Gráfico: Pedidos por Dia do período (max 31 dias no gráfico) ──
        var diasSemana = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };
        var totalDias  = (int)(dataFim - dataInicio).TotalDays + 1;
        var diasGrafico = Math.Min(totalDias, 31);
        var inicioGrafico = dataFim.AddDays(-(diasGrafico - 1));

        var pedidosPorDia = Enumerable.Range(0, diasGrafico)
            .Select(offset =>
            {
                var dia = inicioGrafico.AddDays(offset);
                var pedsDia = entregues
                    .Where(p => p.DataHoraPedido.Date == dia)
                    .ToList();
                return new DiaResumoDto
                {
                    Dia          = diasSemana[(int)dia.DayOfWeek],
                    DataCompleta = dia.ToString("dd/MM"),
                    Pedidos      = pedsDia.Count,
                    Faturamento  = pedsDia.Sum(p => Valor(p))
                };
            })
            .ToList();

        var dto = new RelatorioDto
        {
            // ── Período selecionado ──
            TotalPedidos       = entreguesPeriodo.Count,
            FaturamentoBruto   = faturamentoPeriodo,
            TicketMedio        = ticketMedio,
            DataInicioFiltro   = dataInicio.ToString("yyyy-MM-dd"),
            DataFimFiltro      = dataFim.ToString("yyyy-MM-dd"),
            GastosCompras      = gastosCompras,
            LucroBruto         = lucroBruto,
            OutrasDespesas     = outrasDespesas,
            LucroLiquido       = lucroLiquido,

            // ── Hoje (card fixo) ──
            PedidosHoje        = entreguesHoje.Count,
            FaturamentoHoje    = faturamentoHoje,

            // ── Listas ──
            PorFormaPagamento  = porPagamento,
            TopProdutos        = topProdutos,
            PedidosPorDia      = pedidosPorDia
        };

        return Ok(dto);
    }

    // ─────────────────────────────────────────────────────────────────────
    // GET api/relatorios/exportar-csv?de=2025-02-01&ate=2025-02-28
    // Retorna um arquivo CSV com todos os pedidos entregues do período.
    // ─────────────────────────────────────────────────────────────────────
    [HttpGet("exportar-csv")]
    public async Task<IActionResult> ExportarCsv(
        [FromQuery] string? de  = null,
        [FromQuery] string? ate = null)
    {
        var todos    = (await _pedidoRepository.GetAllAsync()).ToList();
        var entregues = todos.Where(p => p.Status == StatusPedido.Entregue).ToList();

        var agora      = DateTime.UtcNow;
        var dataInicio = ParseData(de)  ?? agora.Date.AddDays(-6);
        var dataFim    = ParseData(ate) ?? agora.Date;
        var dataFimFinal = dataFim.AddDays(1).AddTicks(-1);

        var filtrados = entregues
            .Where(p => p.DataHoraPedido >= dataInicio && p.DataHoraPedido <= dataFimFinal)
            .OrderBy(p => p.DataHoraPedido)
            .ToList();

        var sb = new StringBuilder();
        // Cabeçalho
        sb.AppendLine("ID;Data;Hora;Cliente;Telefone;Bairro;Endereço;Pagamento;Itens;Subtotal;Taxa Entrega;Total");

        foreach (var p in filtrados)
        {
            var itens     = string.Join(" | ", p.Itens.Select(i => $"{i.Quantidade}x {i.Produto.Nome}"));
            var subtotal  = p.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
            var taxa      = p.BairroEntrega?.TaxaEntrega ?? 0m;
            var total     = subtotal + taxa;
            var pagamento = p.MetodoPagamento switch
            {
                MetodoPagamento.Dinheiro      => "Dinheiro",
                MetodoPagamento.CartaoCredito => "Cartão Crédito",
                MetodoPagamento.CartaoDebito  => "Cartão Débito",
                MetodoPagamento.Pix           => "PIX",
                _                             => p.MetodoPagamento.ToString()
            };

            sb.AppendLine(string.Join(";",
                p.Id,
                p.DataHoraPedido.ToString("dd/MM/yyyy"),
                p.DataHoraPedido.ToString("HH:mm"),
                EscapeCsv(p.NomeCliente),
                EscapeCsv(p.TelefoneCliente),
                EscapeCsv(p.BairroEntrega?.Nome ?? ""),
                EscapeCsv(p.EnderecoEntrega),
                pagamento,
                EscapeCsv(itens),
                subtotal.ToString("F2", CultureInfo.InvariantCulture),
                taxa.ToString("F2",     CultureInfo.InvariantCulture),
                total.ToString("F2",    CultureInfo.InvariantCulture)
            ));
        }

        var bytes    = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"relatorio_{dataInicio:yyyy-MM-dd}_{dataFim:yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    // Usa a propriedade do domínio que já desconta ValorCashbackUsado corretamente.
    private static decimal Valor(Pedido p) => p.ValorTotal;

    private static DateTime? ParseData(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        return DateTime.TryParseExact(val, "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private static string EscapeCsv(string val) =>
        val.Contains(';') || val.Contains('"') || val.Contains('\n')
            ? $"\"{val.Replace("\"", "\"\"")}\""
            : val;
}
