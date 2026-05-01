using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class ComplementoTests
{
    private static Complemento CriarComplemento(string nome = "Queijo", decimal preco = 3m,
        string categoriaAlvo = "Todas", string tipoAcao = "AdicionalPago")
        => new(nome, preco, categoriaAlvo, tipoAcao);

    // ── Construtor ──────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ParametrosValidos_CriaComSucesso()
    {
        var complemento = CriarComplemento("Bacon", 5m, "Batatas", "AdicionalPago");

        complemento.Nome.Should().Be("Bacon");
        complemento.Preco.Should().Be(5m);
        complemento.CategoriaAlvo.Should().Be("Batatas");
        complemento.TipoAcao.Should().Be("AdicionalPago");
        complemento.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Construtor_ComCategoriaTodasPadrao_ArmazenaCorretamente()
    {
        var complemento = new Complemento("Maionese", 2m, "Todas", "AdicionalPago");

        complemento.CategoriaAlvo.Should().Be("Todas");
    }

    [Fact]
    public void Construtor_ComTipoAcaoDiferente_ArmazenaCorretamente()
    {
        var complemento = CriarComplemento("Item", 1m, "Porcoes", "Grátis");

        complemento.TipoAcao.Should().Be("Grátis");
    }

    // ── Invariantes ─────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_NomeVazio_CriaComNomeVazio()
    {
        var complemento = CriarComplemento("");

        complemento.Nome.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_PrecoNegativo_CriaComPrecoNegativo()
    {
        var complemento = CriarComplemento(preco: -5m);

        complemento.Preco.Should().Be(-5m);
    }

    [Fact]
    public void Construtor_PrecoZero_CriaComPrecoZero()
    {
        var complemento = CriarComplemento(preco: 0m);

        complemento.Preco.Should().Be(0m);
    }

    [Fact]
    public void Construtor_CategoriaVazia_CriaComCategoriaVazia()
    {
        var complemento = CriarComplemento(categoriaAlvo: "");

        complemento.CategoriaAlvo.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_TipoAcaoVazio_CriaComTipoVazio()
    {
        var complemento = CriarComplemento(tipoAcao: "");

        complemento.TipoAcao.Should().BeEmpty();
    }

    // ── Atualizar ───────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ParametrosValidos_AtualizaComSucesso()
    {
        var complemento = CriarComplemento();

        complemento.Atualizar("Novo Nome", 7.50m, "Porcoes", "Grátis");

        complemento.Nome.Should().Be("Novo Nome");
        complemento.Preco.Should().Be(7.50m);
        complemento.CategoriaAlvo.Should().Be("Porcoes");
        complemento.TipoAcao.Should().Be("Grátis");
    }

    [Fact]
    public void Atualizar_NomeVazio_AtualizaComNomeVazio()
    {
        var complemento = CriarComplemento("Nome Original");

        complemento.Atualizar("", 3m, "Todas", "AdicionalPago");

        complemento.Nome.Should().BeEmpty();
    }

    [Fact]
    public void Atualizar_PrecoNegativo_AtualizaComPrecoNegativo()
    {
        var complemento = CriarComplemento();

        complemento.Atualizar("Teste", -10m, "Todas", "AdicionalPago");

        complemento.Preco.Should().Be(-10m);
    }

    // ── Ativar/Desativar ────────────────────────────────────────────────────

    [Fact]
    public void Ativar_ComplementoAtivo_PermanecerAtivo()
    {
        var complemento = CriarComplemento();

        complemento.Ativar();

        complemento.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Desativar_ComplementoAtivo_FicaInativo()
    {
        var complemento = CriarComplemento();

        complemento.Desativar();

        complemento.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Ativar_ComplementoInativo_FicaAtivo()
    {
        var complemento = CriarComplemento();
        complemento.Desativar();

        complemento.Ativar();

        complemento.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Desativar_MultiplasVezes_PermanecerInativo()
    {
        var complemento = CriarComplemento();

        complemento.Desativar();
        complemento.Desativar();

        complemento.Ativo.Should().BeFalse();
    }
}
