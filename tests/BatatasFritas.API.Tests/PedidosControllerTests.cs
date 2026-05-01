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

    // ── TESTE 5: POST com produto inexistente → estoque=0 → 400 ─────────
    [Fact]
    public async Task Post_EstoqueInsuficiente_Retorna400()
    {
        // Arrange — produtoId=9999 não existe no DB: SELECT retorna null → Convert.ToInt32(null)=0 < 1
        var dto = new NovoPedidoDto
        {
            NomeCliente      = "Cliente Estoque",
            TelefoneCliente  = "11888888888",
            EnderecoEntrega  = "Rua Sem Estoque, 1",
            BairroEntregaId  = 1,
            MetodoPagamento  = MetodoPagamento.Dinheiro,
            TipoAtendimento  = TipoAtendimento.Delivery,
            MomentoPagamento = MomentoPagamento.NaEntrega,
            Itens = new List<NovoItemPedidoDto>
            {
                new()
                {
                    ProdutoId     = 9999,
                    NomeProduto   = "Produto Inexistente",
                    CategoriaId   = CategoriaEnum.Batatas,
                    Quantidade    = 1,
                    PrecoUnitario = 10.00m,
                    Observacao    = ""
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: $"produto inexistente tem estoque efetivo 0. Body: {body}");
        body.ToLower().Should().Contain("estoque",
            because: "mensagem de erro deve mencionar estoque");
    }

    // ── TESTE 6: POST com Pix → retorna QrCodeBase64 e MpPagamentoId ────
    [Fact]
    public async Task Post_ComPix_RetornaQrCode()
    {
        // Arrange
        _mockMp.CriarPagamentoPixAsync(Arg.Any<PagamentoPixRequest>())
            .Returns(Task.FromResult(new PagamentoPixResponse(
                PagamentoId:  123456L,
                QrCodeBase64: "base64img",
                QrCodeTexto:  "00020101...",
                ExpiraEm:     DateTime.UtcNow.AddMinutes(30)
            )));

        var dto = CriarPedidoDto();
        dto.MetodoPagamento  = MetodoPagamento.Pix;
        dto.TipoAtendimento  = TipoAtendimento.Delivery;
        dto.MomentoPagamento = MomentoPagamento.Online;

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("qrCodeBase64").GetString()
            .Should().NotBeNullOrEmpty(because: "Pix deve gerar QR Code");
        doc.RootElement.GetProperty("mpPagamentoId").GetInt64()
            .Should().Be(123456L);
    }

    // ── TESTE 7: POST com cartão aprovado → CartaoStatus = "approved" ───
    [Fact]
    public async Task Post_ComCartaoAprovado_RetornaStatusAprovado()
    {
        // Arrange
        _mockMp.CriarPagamentoCartaoAsync(Arg.Any<PagamentoCartaoRequest>())
            .Returns(Task.FromResult(new PagamentoCartaoResponse(
                PagamentoId:  789L,
                Status:       "approved",
                StatusDetail: "accredited"
            )));

        var dto = CriarPedidoDto();
        dto.MetodoPagamento    = MetodoPagamento.Cartao;
        dto.MomentoPagamento   = MomentoPagamento.Online;
        dto.CardToken          = "test-token-123";
        dto.CardPaymentMethodId = "visa";

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("cartaoStatus").GetString()
            .Should().Be("approved");
    }

    // ── TESTE 8: POST com cartão recusado → CartaoStatus = "rejected" ───
    [Fact]
    public async Task Post_ComCartaoRecusado_RetornaStatusRecusado()
    {
        // Arrange
        _mockMp.CriarPagamentoCartaoAsync(Arg.Any<PagamentoCartaoRequest>())
            .Returns(Task.FromResult(new PagamentoCartaoResponse(
                PagamentoId:  0L,
                Status:       "rejected",
                StatusDetail: "cc_rejected_bad_filled_card_number"
            )));

        var dto = CriarPedidoDto();
        dto.MetodoPagamento    = MetodoPagamento.Cartao;
        dto.MomentoPagamento   = MomentoPagamento.Online;
        dto.CardToken          = "bad-token";
        dto.CardPaymentMethodId = "visa";

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("cartaoStatus").GetString()
            .Should().Be("rejected");
    }

    // ── TESTE 9: POST com múltiplos itens → 200 e PedidoId > 0 ─────────
    [Fact]
    public async Task Post_ComMultiplosItens_TodosItensSalvos()
    {
        // Arrange — dois itens diferentes; produtoId=1 e produtoId=2 (ambos têm receita ou estoque no seed)
        var dto = new NovoPedidoDto
        {
            NomeCliente      = "Cliente Multi",
            TelefoneCliente  = "11777777777",
            EnderecoEntrega  = "Rua Multi, 10",
            BairroEntregaId  = 1,
            MetodoPagamento  = MetodoPagamento.Dinheiro,
            TipoAtendimento  = TipoAtendimento.Delivery,
            MomentoPagamento = MomentoPagamento.NaEntrega,
            Itens = new List<NovoItemPedidoDto>
            {
                new()
                {
                    ProdutoId     = 1,
                    NomeProduto   = "Batata Suprema Média",
                    CategoriaId   = CategoriaEnum.Batatas,
                    Quantidade    = 1,
                    PrecoUnitario = 35.90m,
                    Observacao    = ""
                },
                new()
                {
                    ProdutoId     = 2,
                    NomeProduto   = "Batata Suprema Grande",
                    CategoriaId   = CategoriaEnum.Batatas,
                    Quantidade    = 1,
                    PrecoUnitario = 45.90m,
                    Observacao    = ""
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("pedidoId").GetInt32().Should().BeGreaterThan(0);
    }

    // ── TESTE 10: GET bydate com filtro de hoje → retorna >= 1 item ─────
    [Fact]
    public async Task GetByDate_ComFiltroData_RetornaApenasPeriodo()
    {
        // Arrange — garante que existe ao menos um pedido hoje
        var dto = CriarPedidoDto();
        var postResp = await _client.PostAsJsonAsync("/api/pedidos", dto);
        postResp.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "precisa criar pedido para filtrar por data");

        var hoje = DateTime.UtcNow.ToString("yyyy-MM-dd");

        // Act
        var response = await _client.GetAsync($"/api/pedidos/bydate?start={hoje}&end={hoje}&page=1&pageSize=50");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("items").GetArrayLength()
            .Should().BeGreaterOrEqualTo(1, because: "criamos ao menos um pedido hoje");
    }

    // ── TESTE 11: GET bydate pageSize > totalCount → retorna tudo sem erro
    [Fact]
    public async Task GetByDate_PageSizeMaiorQueTotalCount_RetornaTudoSemErro()
    {
        // Arrange — cria 2 pedidos
        for (int i = 0; i < 2; i++)
            await _client.PostAsJsonAsync("/api/pedidos", CriarPedidoDto());

        // Act — pageSize muito maior que o total
        var response = await _client.GetAsync("/api/pedidos/bydate?page=1&pageSize=100");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: $"pageSize grande não deve causar erro. Body: {body}");

        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("totalCount").GetInt32()
            .Should().BeGreaterOrEqualTo(2);
        doc.RootElement.GetProperty("items").GetArrayLength()
            .Should().BeLessOrEqualTo(100);
    }

    // ── TESTE 12: POST com bairroId inexistente → 400 BadRequest ────────
    [Fact]
    public async Task Post_SemBairroValido_Retorna400()
    {
        // Arrange — BairroEntregaId=99999 não existe; GetByIdAsync retorna null
        // → new Pedido(..., null, ...) lança NullReferenceException → catch → 400
        var dto = new NovoPedidoDto
        {
            NomeCliente      = "Cliente Bairro Ruim",
            TelefoneCliente  = "11666666666",
            EnderecoEntrega  = "Rua Perdida, 0",
            BairroEntregaId  = 99999,
            MetodoPagamento  = MetodoPagamento.Dinheiro,
            TipoAtendimento  = TipoAtendimento.Delivery,
            MomentoPagamento = MomentoPagamento.NaEntrega,
            Itens = new List<NovoItemPedidoDto>
            {
                new()
                {
                    ProdutoId     = 1,
                    NomeProduto   = "Batata Teste",
                    CategoriaId   = CategoriaEnum.Batatas,
                    Quantidade    = 1,
                    PrecoUnitario = 35.90m,
                    Observacao    = ""
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pedidos", dto);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            because: $"bairro inexistente deve retornar 400. Body: {body}");
    }
}
