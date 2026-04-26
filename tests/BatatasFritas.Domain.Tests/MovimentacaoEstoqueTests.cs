using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class MovimentacaoEstoqueTests
{
    private static Insumo CriarInsumo(decimal estoqueInicial = 0m)
    {
        var insumo = new Insumo("Batata", "kg", 5m, 3.50m);
        insumo.EstoqueAtual = estoqueInicial;
        return insumo;
    }

    // ── ValorTotal (propriedade computed) ────────────────────────────────────

    [Fact]
    public void ValorTotal_ValoresPositivos_RetornaQuantidadeVezesUnitario()
    {
        var insumo = CriarInsumo(10m);
        var mov = new MovimentacaoEstoque(insumo, TipoMovimentacao.Entrada, 5m, 10.50m, "Compra");

        mov.ValorTotal.Should().Be(52.50m);
    }

    [Fact]
    public void ValorTotal_QuantidadeZero_Retorna0()
    {
        var insumo = CriarInsumo(10m);
        var mov = new MovimentacaoEstoque(insumo, TipoMovimentacao.Entrada, 0m, 100m, "Teste");

        mov.ValorTotal.Should().Be(0m);
    }

    [Fact]
    public void ValorTotal_ValorUnitarioZero_Retorna0()
    {
        var insumo = CriarInsumo(10m);
        var mov = new MovimentacaoEstoque(insumo, TipoMovimentacao.Entrada, 10m, 0m, "Teste");

        mov.ValorTotal.Should().Be(0m);
    }

    // ── Efeito no estoque do Insumo ──────────────────────────────────────────

    [Fact]
    public void Construtor_TipoEntrada_AumentaEstoqueInsumo()
    {
        var insumo = CriarInsumo(0m);

        new MovimentacaoEstoque(insumo, TipoMovimentacao.Entrada, 10m, 3.50m, "Compra NF-001");

        insumo.EstoqueAtual.Should().Be(10m);
    }

    [Fact]
    public void Construtor_TipoSaida_DiminuiEstoqueInsumo()
    {
        var insumo = CriarInsumo(20m);

        new MovimentacaoEstoque(insumo, TipoMovimentacao.Saida, 5m, 0m, "Uso em pedido");

        insumo.EstoqueAtual.Should().Be(15m);
    }

    [Fact]
    public void Construtor_TipoAjustePositivo_AumentaEstoqueInsumo()
    {
        var insumo = CriarInsumo(10m);

        new MovimentacaoEstoque(insumo, TipoMovimentacao.Ajuste, 8m, 0m, "Ajuste inventário");

        insumo.EstoqueAtual.Should().Be(18m);
    }

    [Fact]
    public void Construtor_TipoAjusteNegativo_DiminuiEstoqueInsumo()
    {
        var insumo = CriarInsumo(10m);

        new MovimentacaoEstoque(insumo, TipoMovimentacao.Ajuste, -3m, 0m, "Descarte");

        insumo.EstoqueAtual.Should().Be(7m);
    }
}
