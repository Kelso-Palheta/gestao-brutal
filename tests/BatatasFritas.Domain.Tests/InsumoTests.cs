using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class InsumoTests
{
    private static Insumo CriarInsumo(string nome = "Óleo", string unidade = "L",
        decimal estoqueMinimo = 5m, decimal custoPorUnidade = 10m)
        => new(nome, unidade, estoqueMinimo, custoPorUnidade);

    // ── Construtor ──────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ParametrosValidos_CriaComSucesso()
    {
        var insumo = CriarInsumo("Batata Congelada", "kg", 20m, 5.50m);

        insumo.Nome.Should().Be("Batata Congelada");
        insumo.Unidade.Should().Be("kg");
        insumo.EstoqueMinimo.Should().Be(20m);
        insumo.CustoPorUnidade.Should().Be(5.50m);
        insumo.EstoqueAtual.Should().Be(0m);
        insumo.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Construtor_UnidadeEmGramas_ArmazenaCorretamente()
    {
        var insumo = CriarInsumo("Sal", "g");

        insumo.Unidade.Should().Be("g");
    }

    [Fact]
    public void Construtor_UnidadeEmUnidades_ArmazenaCorretamente()
    {
        var insumo = CriarInsumo("Ovos", "un");

        insumo.Unidade.Should().Be("un");
    }

    // ── Invariantes ─────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_NomeVazio_CriaComNomeVazio()
    {
        var insumo = CriarInsumo("");

        insumo.Nome.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_CustoPorUnidadeNegativo_CriaComCustoNegativo()
    {
        var insumo = CriarInsumo(custoPorUnidade: -10m);

        insumo.CustoPorUnidade.Should().Be(-10m);
    }

    [Fact]
    public void Construtor_CustoPorUnidadeZero_CriaComCustoZero()
    {
        var insumo = CriarInsumo(custoPorUnidade: 0m);

        insumo.CustoPorUnidade.Should().Be(0m);
    }

    [Fact]
    public void Construtor_EstoqueMinmoNegativo_CriaComEstoqueMinNegativo()
    {
        var insumo = CriarInsumo(estoqueMinimo: -5m);

        insumo.EstoqueMinimo.Should().Be(-5m);
    }

    // ── Atualizar ───────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ParametrosValidos_AtualizaComSucesso()
    {
        var insumo = CriarInsumo();

        insumo.Atualizar("Novo Nome", "kg", 30m, 12.50m);

        insumo.Nome.Should().Be("Novo Nome");
        insumo.Unidade.Should().Be("kg");
        insumo.EstoqueMinimo.Should().Be(30m);
        insumo.CustoPorUnidade.Should().Be(12.50m);
    }

    [Fact]
    public void Atualizar_NomeVazio_AtualizaComNomeVazio()
    {
        var insumo = CriarInsumo("Nome Original");

        insumo.Atualizar("", "L", 5m, 10m);

        insumo.Nome.Should().BeEmpty();
    }

    // ── AbaixoDoMinimo ──────────────────────────────────────────────────────

    [Fact]
    public void AbaixoDoMinimo_EstoqueZeroMinimoPositivo_RetornaTrue()
    {
        var insumo = CriarInsumo(estoqueMinimo: 10m);
        insumo.EstoqueAtual = 0m;

        insumo.AbaixoDoMinimo.Should().BeTrue();
    }

    [Fact]
    public void AbaixoDoMinimo_EstoqueIgualMinimo_RetornaTrue()
    {
        var insumo = CriarInsumo(estoqueMinimo: 10m);
        insumo.EstoqueAtual = 10m;

        insumo.AbaixoDoMinimo.Should().BeTrue();
    }

    [Fact]
    public void AbaixoDoMinimo_EstoqueAcimaMinimo_RetornaFalse()
    {
        var insumo = CriarInsumo(estoqueMinimo: 10m);
        insumo.EstoqueAtual = 15m;

        insumo.AbaixoDoMinimo.Should().BeFalse();
    }

    [Fact]
    public void AbaixoDoMinimo_EstoqueMuitoAcimaMinimo_RetornaFalse()
    {
        var insumo = CriarInsumo(estoqueMinimo: 5m);
        insumo.EstoqueAtual = 100m;

        insumo.AbaixoDoMinimo.Should().BeFalse();
    }

    // ── AjustarEstoque ──────────────────────────────────────────────────────

    [Fact]
    public void AjustarEstoque_AdicionarPositivo_AumentaEstoque()
    {
        var insumo = CriarInsumo();
        insumo.EstoqueAtual = 10m;

        insumo.AjustarEstoque(5m);

        insumo.EstoqueAtual.Should().Be(15m);
    }

    [Fact]
    public void AjustarEstoque_RemoverNegativo_DiminuiEstoque()
    {
        var insumo = CriarInsumo();
        insumo.EstoqueAtual = 20m;

        insumo.AjustarEstoque(-5m);

        insumo.EstoqueAtual.Should().Be(15m);
    }

    [Fact]
    public void AjustarEstoque_ZeroNaoAltera_EstoqueNaoMuda()
    {
        var insumo = CriarInsumo();
        insumo.EstoqueAtual = 10m;

        insumo.AjustarEstoque(0m);

        insumo.EstoqueAtual.Should().Be(10m);
    }

    [Fact]
    public void AjustarEstoque_PodeFicarNegativo_AcumulaCorremente()
    {
        var insumo = CriarInsumo();
        insumo.EstoqueAtual = 5m;

        insumo.AjustarEstoque(-10m);

        insumo.EstoqueAtual.Should().Be(-5m);
    }

    // ── Ativo ───────────────────────────────────────────────────────────────

    [Fact]
    public void Ativo_PadraoTrue_RefleteValorPadrao()
    {
        var insumo = CriarInsumo();

        insumo.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Ativo_PoderSetarFalso_AtualizaAtivo()
    {
        var insumo = CriarInsumo();

        insumo.Ativo = false;

        insumo.Ativo.Should().BeFalse();
    }
}
