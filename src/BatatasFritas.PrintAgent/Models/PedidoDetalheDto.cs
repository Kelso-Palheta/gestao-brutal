namespace BatatasFritas.PrintAgent.Models;

// DTO local — espelha o response de GET /api/pedidos/{id}
// Não referencia BatatasFritas.Shared para manter o PrintAgent standalone.

public class PedidoDetalheDto
{
    public int     Id              { get; set; }
    public string  NomeCliente     { get; set; } = string.Empty;
    public string  TelefoneCliente { get; set; } = string.Empty;
    public string  EnderecoEntrega { get; set; } = string.Empty;
    public string  NomeBairro      { get; set; } = string.Empty;
    public DateTime DataHoraPedido { get; set; }
    public string  Status          { get; set; } = string.Empty;
    public string  MetodoPagamento { get; set; } = string.Empty;
    public string? SegundoMetodoPagamento { get; set; }
    public decimal? ValorSegundoPagamento  { get; set; }
    public string  StatusPagamento { get; set; } = string.Empty;
    public string  TipoAtendimento { get; set; } = string.Empty;
    public decimal? TrocoPara      { get; set; }
    public decimal SubtotalItens   { get; set; }
    public decimal TaxaEntrega     { get; set; }
    public decimal ValorCashbackUsado { get; set; }
    public decimal ValorTotal      { get; set; }
    public List<ItemPedidoDetalheDto> Itens { get; set; } = new();
}

public class ItemPedidoDetalheDto
{
    public int    Id          { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public int    Quantidade  { get; set; }
    public string Observacao  { get; set; } = string.Empty;
}
