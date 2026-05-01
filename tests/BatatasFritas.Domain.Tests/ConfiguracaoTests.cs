using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class ConfiguracaoTests
{
    private static Configuracao CriarConfiguracao(string chave = "senha_kds", string valor = "hash_bcrypt")
        => new(chave, valor);

    // ── Construtor ──────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ParametrosValidos_CriaComSucesso()
    {
        var config = CriarConfiguracao("api_key", "secret_token_123");

        config.Chave.Should().Be("api_key");
        config.Valor.Should().Be("secret_token_123");
    }

    [Fact]
    public void Construtor_ComChaveSenhaKds_ArmazenaCorretamente()
    {
        var config = CriarConfiguracao("senha_kds", "$2b$12$hash_bcrypt");

        config.Chave.Should().Be("senha_kds");
        config.Valor.Should().Be("$2b$12$hash_bcrypt");
    }

    [Fact]
    public void Construtor_ComChaveCustomizada_ArmazenaCorretamente()
    {
        var config = CriarConfiguracao("tempo_sessao", "3600");

        config.Chave.Should().Be("tempo_sessao");
        config.Valor.Should().Be("3600");
    }

    // ── Invariantes ─────────────────────────────────────────────────────────

    [Fact]
    public void Construtor_ChaveVazia_CriaComChaveVazia()
    {
        var config = CriarConfiguracao("");

        config.Chave.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_ValorVazio_CriaComValorVazio()
    {
        var config = CriarConfiguracao("chave", "");

        config.Valor.Should().BeEmpty();
    }

    [Fact]
    public void Construtor_AmbosvVazios_CriaComAmbosVazios()
    {
        var config = CriarConfiguracao("", "");

        config.Chave.Should().BeEmpty();
        config.Valor.Should().BeEmpty();
    }

    // ── Editar Valor ────────────────────────────────────────────────────────

    [Fact]
    public void Valor_PoderSetarNovo_AtualizaValor()
    {
        var config = CriarConfiguracao("chave", "valor_antigo");

        config.Valor = "valor_novo";

        config.Valor.Should().Be("valor_novo");
    }

    [Fact]
    public void Valor_PoderSetarVazio_AtualizaParaVazio()
    {
        var config = CriarConfiguracao("chave", "valor_original");

        config.Valor = "";

        config.Valor.Should().BeEmpty();
    }

    [Fact]
    public void Valor_MultiplasAtualizacoes_RefleteUltimo()
    {
        var config = CriarConfiguracao("chave", "valor1");

        config.Valor = "valor2";
        config.Valor = "valor3";
        config.Valor = "valor_final";

        config.Valor.Should().Be("valor_final");
    }

    // ── Chave (readonly) ────────────────────────────────────────────────────

    [Fact]
    public void Chave_EhImutavel_NaoMuda()
    {
        var config = CriarConfiguracao("chave_original", "valor");

        config.Chave.Should().Be("chave_original");
    }

    [Fact]
    public void Chave_MultiplosConfiguradores_CadaUmTemSuaChave()
    {
        var config1 = CriarConfiguracao("chave1", "valor1");
        var config2 = CriarConfiguracao("chave2", "valor2");

        config1.Chave.Should().Be("chave1");
        config2.Chave.Should().Be("chave2");
    }
}
