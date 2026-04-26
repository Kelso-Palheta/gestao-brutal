using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.Enums;
using FluentAssertions;
using Xunit;

namespace BatatasFritas.Domain.Tests;

public class PedidoTests
{
    private static Pedido CriarPedido(Bairro? bairro = null, decimal cashbackUsado = 0m)
        => new("Cliente", "11999999999", "Rua A, 1", bairro, MetodoPagamento.Dinheiro,
               valorCashbackUsado: cashbackUsado);

    private static Produto CriarProduto(CategoriaEnum categoria, decimal preco = 10m)
        => new("Produto", "Desc", categoria, preco);

    // ── ValorTotalItens ──────────────────────────────────────────────────────

    [Fact]
    public void ValorTotalItens_SemItens_Retorna0()
    {
        var pedido = CriarPedido();

        pedido.ValorTotalItens.Should().Be(0m);
    }

    [Fact]
    public void ValorTotalItens_UmItem_RetornaPrecoVezesQuantidade()
    {
        var pedido = CriarPedido();
        var produto = CriarProduto(CategoriaEnum.Batatas, 10m);

        pedido.AdicionarItem(produto, 2, 10m);

        pedido.ValorTotalItens.Should().Be(20m);
    }

    [Fact]
    public void ValorTotalItens_MultiplosItens_RetornaSomaCorreta()
    {
        var pedido = CriarPedido();
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas, 5m), 3, 5m);
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Bebidas, 10m), 1, 10m);

        pedido.ValorTotalItens.Should().Be(25m);
    }

    // ── ValorElegivelCashback ────────────────────────────────────────────────

    [Fact]
    public void ValorElegivelCashback_SemItensBatatasPorcoes_Retorna0()
    {
        var pedido = CriarPedido();
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Bebidas, 10m), 3, 10m);

        pedido.ValorElegivelCashback.Should().Be(0m);
    }

    [Fact]
    public void ValorElegivelCashback_ApenasItensBatatas_RetornaTotal()
    {
        var pedido = CriarPedido();
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas, 8m), 2, 8m);
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas, 8m), 2, 8m);

        pedido.ValorElegivelCashback.Should().Be(32m);
    }

    [Fact]
    public void ValorElegivelCashback_ApenasItensPorcoes_RetornaTotal()
    {
        var pedido = CriarPedido();
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Porcoes, 15m), 1, 15m);

        pedido.ValorElegivelCashback.Should().Be(15m);
    }

    [Fact]
    public void ValorElegivelCashback_MisturaCategoria_RetornaApenasElegiveis()
    {
        var pedido = CriarPedido();
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas, 10m), 2, 10m); // 20
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Porcoes, 5m), 1, 5m);   // 5
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Bebidas, 3m), 5, 3m);   // 15 (não elegível)

        pedido.ValorElegivelCashback.Should().Be(25m);
    }

    // ── ValorTotal ───────────────────────────────────────────────────────────

    [Fact]
    public void ValorTotal_SemTaxaSemCashback_IgualAValorItens()
    {
        var pedido = CriarPedido();
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas), 10, 10m);

        pedido.ValorTotal.Should().Be(100m);
    }

    [Fact]
    public void ValorTotal_ComTaxaEntrega_SomaCorretamente()
    {
        var bairro = new Bairro("Centro", 15m);
        var pedido = CriarPedido(bairro);
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas), 10, 10m);

        pedido.ValorTotal.Should().Be(115m);
    }

    [Fact]
    public void ValorTotal_ComCashback_DescontaCorretamente()
    {
        var pedido = CriarPedido(cashbackUsado: 30m);
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas), 10, 10m);

        pedido.ValorTotal.Should().Be(70m);
    }

    [Fact]
    public void ValorTotal_CashbackMaiorQueSubtotal_NaoRetornaNegativo()
    {
        var pedido = CriarPedido(cashbackUsado: 70m);
        pedido.AdicionarItem(CriarProduto(CategoriaEnum.Batatas), 5, 10m); // 50

        pedido.ValorTotal.Should().Be(0m);
    }

    // ── AdicionarItem ────────────────────────────────────────────────────────

    [Fact]
    public void AdicionarItem_ItemValido_AdicionaALista()
    {
        var pedido = CriarPedido();
        var produto = CriarProduto(CategoriaEnum.Batatas);

        pedido.AdicionarItem(produto, 3, 10m);

        pedido.Itens.Should().HaveCount(1);
        pedido.Itens[0].Quantidade.Should().Be(3);
        pedido.Itens[0].PrecoUnitario.Should().Be(10m);
    }
}
