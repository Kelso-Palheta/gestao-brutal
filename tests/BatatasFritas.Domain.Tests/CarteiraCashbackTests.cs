using System;
using System.Linq;
using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class CarteiraCashbackTests
{
    [Fact]
    public void AdicionarSaldo_ValorPositivo_AumentaSaldoECriaTransacaoEntrada()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");

        carteira.AdicionarSaldo(10m, "Compra #1");

        carteira.SaldoAtual.Should().Be(10m);
        carteira.Transacoes.Should().HaveCount(1);
        carteira.Transacoes.First().Valor.Should().Be(10m);
        carteira.Transacoes.First().Tipo.Should().Be(TipoTransacaoCashback.Entrada);
    }

    [Fact]
    public void AdicionarSaldo_ValorZero_LancaArgumentException()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");

        Action act = () => carteira.AdicionarSaldo(0m, "teste");

        act.Should().Throw<ArgumentException>()
           .WithMessage("Valor para adicionar deve ser maior que zero.");
    }

    [Fact]
    public void AdicionarSaldo_ValorNegativo_LancaArgumentException()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");

        Action act = () => carteira.AdicionarSaldo(-5m, "teste");

        act.Should().Throw<ArgumentException>()
           .WithMessage("Valor para adicionar deve ser maior que zero.");
    }

    [Fact]
    public void UsarSaldo_SaldoSuficiente_DiminuiSaldoECriaTransacaoSaida()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(20m, "Carga inicial");

        carteira.UsarSaldo(10m, "Resgate pedido #2");

        carteira.SaldoAtual.Should().Be(10m);
        carteira.Transacoes.Should().HaveCount(2);
        carteira.Transacoes.Last().Valor.Should().Be(10m);
        carteira.Transacoes.Last().Tipo.Should().Be(TipoTransacaoCashback.Saida);
    }

    [Fact]
    public void UsarSaldo_SaldoInsuficiente_LancaInvalidOperationException()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(5m, "Carga inicial");

        Action act = () => carteira.UsarSaldo(10m, "Resgate");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("Saldo insuficiente na carteira de cashback.");
    }

    [Fact]
    public void UsarSaldo_ValorZero_LancaArgumentException()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(10m, "Carga inicial");

        Action act = () => carteira.UsarSaldo(0m, "teste");

        act.Should().Throw<ArgumentException>()
           .WithMessage("Valor para usar deve ser maior que zero.");
    }

    [Fact]
    public void UsarSaldo_ValorNegativo_LancaArgumentException()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(10m, "Carga inicial");

        Action act = () => carteira.UsarSaldo(-5m, "teste");

        act.Should().Throw<ArgumentException>()
           .WithMessage("Valor para usar deve ser maior que zero.");
    }

    [Fact]
    public void SetSaldoManual_AumentoDeSaldo_CriaTransacaoEntrada()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(10m, "Carga inicial");

        carteira.SetSaldoManual(20m, "Ajuste admin");

        carteira.SaldoAtual.Should().Be(20m);
        carteira.Transacoes.Should().HaveCount(2);
        carteira.Transacoes.Last().Tipo.Should().Be(TipoTransacaoCashback.Entrada);
        carteira.Transacoes.Last().Valor.Should().Be(10m);
    }

    [Fact]
    public void SetSaldoManual_ReducaoDeSaldo_CriaTransacaoSaida()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(20m, "Carga inicial");

        carteira.SetSaldoManual(10m, "Ajuste admin");

        carteira.SaldoAtual.Should().Be(10m);
        carteira.Transacoes.Last().Tipo.Should().Be(TipoTransacaoCashback.Saida);
        carteira.Transacoes.Last().Valor.Should().Be(10m);
    }

    [Fact]
    public void SetSaldoManual_MesmoValor_NaoCriaTransacao()
    {
        var carteira = new CarteiraCashback("11999999999", "Cliente Teste");
        carteira.AdicionarSaldo(15m, "Carga inicial");

        carteira.SetSaldoManual(15m, "Ajuste sem mudança");

        carteira.SaldoAtual.Should().Be(15m);
        carteira.Transacoes.Should().HaveCount(1); // apenas a inicial
    }
}
