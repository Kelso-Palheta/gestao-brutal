using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class ProdutoTests
{
    private static Produto CriarProduto(string nome = "Batata Frita", string descricao = "Desc",
        CategoriaEnum categoria = CategoriaEnum.Batatas, decimal precoBase = 10m,
        string imagemUrl = "", string complementosPermitidos = "",
        int estoqueAtual = 0, int estoqueMinimo = 0)
        => new(nome, descricao, categoria, precoBase, imagemUrl, complementosPermitidos, estoqueAtual, estoqueMinimo);

    // ── Construtor ──────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ParametrosValidos_CriaComSucesso()
    {
        var produto = CriarProduto("Batata Grande", "Descrição", CategoriaEnum.Batatas, 15m);

        produto.Nome.Should().Be("Batata Grande");
        produto.Descricao.Should().Be("Descrição");
        produto.CategoriaId.Should().Be(CategoriaEnum.Batatas);
        produto.PrecoBase.Should().Be(15m);
        produto.Ativo.Should().BeTrue();
        produto.EstoqueAtual.Should().Be(0);
        produto.EstoqueMinimo.Should().Be(0);
    }

    [Fact]
    public void Construtor_ComComplementosEEstoque_CriaComSucesso()
    {
        var produto = CriarProduto("Produto", "Desc", CategoriaEnum.Batatas, 10m, "", "1,2,3", 50, 10);

        produto.ComplementosPermitidos.Should().Be("1,2,3");
        produto.EstoqueAtual.Should().Be(50);
        produto.EstoqueMinimo.Should().Be(10);
    }

    [Fact]
    public void Construtor_ComImagemUrl_ArmazenaCorretamente()
    {
        var produto = CriarProduto("Produto", "Desc", CategoriaEnum.Batatas, 10m, "https://exemplo.com/img.jpg");

        produto.ImagemUrl.Should().Be("https://exemplo.com/img.jpg");
    }

    // ── Invariantes ─────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_NomeVazio_CriaComNomeVazio()
    {
        var produto = CriarProduto("");

        produto.Nome.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_PrecoNegativo_CriaComPrecoNegativo()
    {
        var produto = CriarProduto(precoBase: -10m);

        produto.PrecoBase.Should().Be(-10m);
    }

    [Fact]
    public void Construtor_PrecoZero_CriaComPrecoZero()
    {
        var produto = CriarProduto(precoBase: 0m);

        produto.PrecoBase.Should().Be(0m);
    }

    [Fact]
    public void Construtor_EstoqueNegativo_CriaComEstoqueNegativo()
    {
        var produto = CriarProduto(estoqueAtual: -5);

        produto.EstoqueAtual.Should().Be(-5);
    }

    // ── Atualizar ───────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ParametrosValidos_AtualizaComSucesso()
    {
        var produto = CriarProduto();

        produto.Atualizar("Novo Nome", "Nova Desc", CategoriaEnum.Porcoes, 20m, "url", "1,2");

        produto.Nome.Should().Be("Novo Nome");
        produto.Descricao.Should().Be("Nova Desc");
        produto.CategoriaId.Should().Be(CategoriaEnum.Porcoes);
        produto.PrecoBase.Should().Be(20m);
        produto.ImagemUrl.Should().Be("url");
        produto.ComplementosPermitidos.Should().Be("1,2");
    }

    [Fact]
    public void Atualizar_NomeVazio_AtualizaComNomeVazio()
    {
        var produto = CriarProduto("Nome Original");

        produto.Atualizar("", "Desc", CategoriaEnum.Batatas, 10m, "");

        produto.Nome.Should().BeEmpty();
    }

    // ── Ativar/Desativar ────────────────────────────────────────────────────

    [Fact]
    public void Ativar_ProdutoAtivo_PermanecerAtivo()
    {
        var produto = CriarProduto();

        produto.Ativar();

        produto.Ativo.Should().BeTrue();
    }

    [Fact]
    public void Desativar_ProdutoAtivo_FicaInativo()
    {
        var produto = CriarProduto();

        produto.Desativar();

        produto.Ativo.Should().BeFalse();
    }

    [Fact]
    public void Ativar_ProdutoInativo_FicaAtivo()
    {
        var produto = CriarProduto();
        produto.Desativar();

        produto.Ativar();

        produto.Ativo.Should().BeTrue();
    }

    // ── AjustarEstoque ──────────────────────────────────────────────────────

    [Fact]
    public void AjustarEstoque_AdicionarPositivo_AumentaEstoque()
    {
        var produto = CriarProduto(estoqueAtual: 10);

        produto.AjustarEstoque(5);

        produto.EstoqueAtual.Should().Be(15);
    }

    [Fact]
    public void AjustarEstoque_RemoverNegativo_DiminuiEstoque()
    {
        var produto = CriarProduto(estoqueAtual: 10);

        produto.AjustarEstoque(-3);

        produto.EstoqueAtual.Should().Be(7);
    }

    [Fact]
    public void AjustarEstoque_ZeroNaoAltera_EstoqueNaoMuda()
    {
        var produto = CriarProduto(estoqueAtual: 10);

        produto.AjustarEstoque(0);

        produto.EstoqueAtual.Should().Be(10);
    }

    [Fact]
    public void AjustarEstoque_MuliplasVezes_AcumulaCorretamente()
    {
        var produto = CriarProduto(estoqueAtual: 100);

        produto.AjustarEstoque(50);
        produto.AjustarEstoque(-30);
        produto.AjustarEstoque(10);

        produto.EstoqueAtual.Should().Be(130);
    }

    // ── Ordem ───────────────────────────────────────────────────────────────

    [Fact]
    public void Ordem_PadraoZero_RefleteValorPadrao()
    {
        var produto = CriarProduto();

        produto.Ordem.Should().Be(0);
    }

    [Fact]
    public void Ordem_PoderSetarValor_AtualizaOrdem()
    {
        var produto = CriarProduto();

        produto.Ordem = 5;

        produto.Ordem.Should().Be(5);
    }
}
