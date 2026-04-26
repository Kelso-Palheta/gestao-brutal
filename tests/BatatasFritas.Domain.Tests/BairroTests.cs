using BatatasFritas.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class BairroTests
{
    [Fact]
    public void AtualizarTaxa_TaxaValida_AtualizaCorretamente()
    {
        var bairro = new Bairro("Centro", 5m);

        bairro.AtualizarTaxa(15.50m);

        bairro.TaxaEntrega.Should().Be(15.50m);
    }

    [Fact]
    public void AtualizarTaxa_TaxaZero_PermiteZero()
    {
        var bairro = new Bairro("Centro", 5m);

        bairro.AtualizarTaxa(0m);

        bairro.TaxaEntrega.Should().Be(0m);
    }

    [Fact]
    public void AtualizarTaxa_TaxaNegativa_AceitaSemValidacao()
    {
        var bairro = new Bairro("Centro", 5m);

        bairro.AtualizarTaxa(-5.50m);

        bairro.TaxaEntrega.Should().Be(-5.50m);
    }

    [Fact]
    public void Atualizar_NomeETaxa_AtualizaAmbos()
    {
        var bairro = new Bairro("Centro", 5m);

        bairro.Atualizar("Jardim", 12m);

        bairro.Nome.Should().Be("Jardim");
        bairro.TaxaEntrega.Should().Be(12m);
    }

    [Fact]
    public void Atualizar_MesmoNomeNovaTaxa_AtualizaApenasATaxa()
    {
        var bairro = new Bairro("Centro", 5m);

        bairro.Atualizar("Centro", 18m);

        bairro.Nome.Should().Be("Centro");
        bairro.TaxaEntrega.Should().Be(18m);
    }
}
