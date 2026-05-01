using System;
using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class DespesaTests
{
    private static Despesa CriarDespesa(string descricao = "Almoço funcionários", decimal valor = 150m,
        DateTime? dataRegistro = null, string categoria = "Funcionario", string? observacao = null)
    {
        dataRegistro ??= DateTime.Now;
        return new(descricao, valor, dataRegistro.Value, categoria, observacao);
    }

    // ── Construtor ──────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ParametrosValidos_CriaComSucesso()
    {
        var data = new DateTime(2026, 04, 30);
        var despesa = CriarDespesa("Aluguel", 2000m, data, "Aluguel");

        despesa.Descricao.Should().Be("Aluguel");
        despesa.Valor.Should().Be(2000m);
        despesa.DataRegistro.Should().Be(data);
        despesa.Categoria.Should().Be("Aluguel");
        despesa.Observacao.Should().BeNull();
    }

    [Fact]
    public void Construtor_ComObservacao_ArmazenaCorretamente()
    {
        var data = new DateTime(2026, 04, 30);
        var despesa = CriarDespesa("Energia", 500m, data, "Energia/Agua", "Fatura abril");

        despesa.Observacao.Should().Be("Fatura abril");
    }

    [Fact]
    public void Construtor_CategoriaFuncionario_ArmazenaCorretamente()
    {
        var despesa = CriarDespesa(categoria: "Funcionario");

        despesa.Categoria.Should().Be("Funcionario");
    }

    [Fact]
    public void Construtor_CategoriaImposto_ArmazenaCorretamente()
    {
        var despesa = CriarDespesa(categoria: "Imposto");

        despesa.Categoria.Should().Be("Imposto");
    }

    [Fact]
    public void Construtor_CategoriaOutros_ArmazenaCorretamente()
    {
        var despesa = CriarDespesa(categoria: "Outros");

        despesa.Categoria.Should().Be("Outros");
    }

    // ── Invariantes ─────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_DescricaoVazia_CriaComDescricaoVazia()
    {
        var despesa = CriarDespesa("");

        despesa.Descricao.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_ValorNegativo_CriaComValorNegativo()
    {
        var despesa = CriarDespesa(valor: -100m);

        despesa.Valor.Should().Be(-100m);
    }

    [Fact]
    public void Construtor_ValorZero_CriaComValorZero()
    {
        var despesa = CriarDespesa(valor: 0m);

        despesa.Valor.Should().Be(0m);
    }

    [Fact]
    public void Construtor_CategoriaVazia_CriaComCategoriaVazia()
    {
        var despesa = CriarDespesa(categoria: "");

        despesa.Categoria.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_DataNoPassado_ArmazenaCorretamente()
    {
        var dataPassado = DateTime.Now.AddDays(-10);
        var despesa = CriarDespesa(dataRegistro: dataPassado);

        despesa.DataRegistro.Should().Be(dataPassado);
    }

    [Fact]
    public void Construtor_ObservacaoVazia_CriaComObservacaoVazia()
    {
        var despesa = CriarDespesa(observacao: "");

        despesa.Observacao.Should().Be("");
    }

    // ── Atualizar ───────────────────────────────────────────────────────────

    [Fact]
    public void Atualizar_ParametrosValidos_AtualizaComSucesso()
    {
        var despesa = CriarDespesa();
        var novaData = new DateTime(2026, 05, 01);

        despesa.Atualizar("Nova Desc", 300m, novaData, "Imposto", "Nota fiscal");

        despesa.Descricao.Should().Be("Nova Desc");
        despesa.Valor.Should().Be(300m);
        despesa.DataRegistro.Should().Be(novaData);
        despesa.Categoria.Should().Be("Imposto");
        despesa.Observacao.Should().Be("Nota fiscal");
    }

    [Fact]
    public void Atualizar_DescricaoVazia_AtualizaComDescricaoVazia()
    {
        var despesa = CriarDespesa("Desc Original");

        despesa.Atualizar("", 100m, DateTime.Now, "Outros");

        despesa.Descricao.Should().BeEmpty();
    }

    [Fact]
    public void Atualizar_ValorNegativo_AtualizaComValorNegativo()
    {
        var despesa = CriarDespesa();

        despesa.Atualizar("Desc", -500m, DateTime.Now, "Outros");

        despesa.Valor.Should().Be(-500m);
    }

    [Fact]
    public void Atualizar_ObservacaoParaNull_AtualizaCorretamente()
    {
        var despesa = CriarDespesa(observacao: "Observacao Original");

        despesa.Atualizar("Desc", 100m, DateTime.Now, "Outros", null);

        despesa.Observacao.Should().BeNull();
    }

    [Fact]
    public void Atualizar_MultiplasVezes_RefleteUltimosValores()
    {
        var despesa = CriarDespesa("Desc1", 100m);

        despesa.Atualizar("Desc2", 200m, DateTime.Now, "Categoria1");
        despesa.Atualizar("Desc3", 300m, DateTime.Now, "Categoria2");

        despesa.Descricao.Should().Be("Desc3");
        despesa.Valor.Should().Be(300m);
        despesa.Categoria.Should().Be("Categoria2");
    }

    // ── Campos obrigatórios ──────────────────────────────────────────────────

    [Fact]
    public void Construtor_DataRegistroAceita_Hoje()
    {
        var hoje = DateTime.Now.Date;
        var despesa = CriarDespesa(dataRegistro: hoje);

        despesa.DataRegistro.Date.Should().Be(hoje);
    }
}
