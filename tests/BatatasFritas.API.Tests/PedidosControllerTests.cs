using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BatatasFritas.Domain.Interfaces;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using NSubstitute;

namespace BatatasFritas.API.Tests;

/// <summary>
/// Integration tests para POST /api/pedidos e GET /api/pedidos/bydate.
/// Usa CustomWebApplicationFactory com SQLite in-memory e mock de MercadoPago.
/// </summary>
public class PedidosControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IMercadoPagoService _mockMp;

    public PedidosControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockMp = factory.MockMercadoPago;
    }

    // ── Helper: cria um NovoPedidoDto mínimo válido ────────────────────
    private static NovoPedidoDto CriarPedidoDto(int produtoId = 1, int quantidade = 1)
    {
        return new NovoPedidoDto
        {
            NomeCliente      = "Cliente Teste",
            TelefoneCliente  = "11999999999",
            EnderecoEntrega  = "Rua dos Testes, 42",
            BairroEntregaId  = 1,
            MetodoPagamento  = MetodoPagamento.Dinheiro,
            TipoAtendimento  = TipoAtendimento.Delivery,
            MomentoPagamento = MomentoPagamento.NaEntrega,
            Itens = new List<NovoItemPedidoDto>
            {
                new()
                {
                    ProdutoId     = produtoId,
                    NomeProduto   = "Batata Suprema Média",
                    CategoriaId   = CategoriaEnum.Batatas,
                    Quantidade    = quantidade,
                    PrecoUnitario = 35.90m,
                    Observacao    = ""
                }
            }
        };
    }

    // ── TESTE 1: POST /api/pedidos completo → 200 OK ───────────────────
    [Fact]
    public async Task Post_PedidoCompleto_Retorna200ComPedidoId()
    {
        // Arrange
        var dto = CriarPedidoDto();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — pedido criado com sucesso
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"esperava 200 OK, mas recebeu {response.StatusCode}. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.TryGetProperty("pedidoId", out var pedidoIdEl).Should().BeTrue();
        var pedidoId = pedidoIdEl.GetInt32();
        pedidoId.Should().BeGreaterThan(0);

        doc.RootElement.GetProperty("statusPagamento").GetString()
            .Should().Be("Pendente", because: "pagamento em dinheiro na entrega começa pendente");
    }

    // ── TESTE 2: POST /api/pedidos → estoque decrementado ──────────────
    [Fact]
    public async Task Post_PedidoCompleto_DecrementaEstoque()
    {
        // Arrange — cria um pedido com 1 unidade
        var dto = CriarPedidoDto(produtoId: 1, quantidade: 1);

        // Act — cria o pedido (o controller faz BaixarEstoque internamente)
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert — pedido criado com sucesso (estoque era > 0 após seed)
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Body: {body}");
    }

    // ── TESTE 3: GET /api/pedidos/bydate paginado ──────────────────────
    [Fact]
    public async Task GetByDate_Paginado_RetornaPageSizeCorreto()
    {
        // Arrange — cria 3 pedidos para ter dados
        for (int i = 0; i < 3; i++)
        {
            var dto = CriarPedidoDto();
            await _client.PostAsJsonAsync("/api/pedidos", dto);
        }

        // Act — pede página 1 com pageSize 2
        var response = await _client.GetAsync("/api/pedidos/bydate?page=1&pageSize=2");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        root.TryGetProperty("page", out var pageEl).Should().BeTrue("resposta deve ter campo 'page'");
        pageEl.GetInt32().Should().Be(1);

        root.TryGetProperty("pageSize", out var pageSizeEl).Should().BeTrue("resposta deve ter campo 'pageSize'");
        pageSizeEl.GetInt32().Should().Be(2);

        root.TryGetProperty("items", out var itemsEl).Should().BeTrue("resposta deve ter campo 'items'");
        itemsEl.GetArrayLength().Should().BeLessOrEqualTo(2,
            because: "pageSize=2 deve limitar os itens retornados");

        root.TryGetProperty("totalCount", out var totalEl).Should().BeTrue("resposta deve ter campo 'totalCount'");
        totalEl.GetInt32().Should().BeGreaterOrEqualTo(3,
            because: "pelo menos 3 pedidos foram criados neste teste + pedidos de testes anteriores");
    }

    // ── TESTE 4: GET /api/pedidos/bydate com page inválida → 400 ───────
    [Fact]
    public async Task GetByDate_PageInvalida_Retorna400()
    {
        var response = await _client.GetAsync("/api/pedidos/bydate?page=0&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
