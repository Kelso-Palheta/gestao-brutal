using System.Collections.Generic;
using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Shared.DTOs;

public class NovoPedidoDto
{
    public string NomeCliente { get; set; } = string.Empty;
    public string TelefoneCliente { get; set; } = string.Empty;
    public string EnderecoEntrega { get; set; } = string.Empty;
    public int BairroEntregaId { get; set; }
    public MetodoPagamento MetodoPagamento { get; set; }
    public MetodoPagamento? SegundoMetodoPagamento { get; set; }
    public decimal? ValorSegundoPagamento { get; set; }
    public decimal? TrocoPara { get; set; }
    public TipoAtendimento TipoAtendimento { get; set; } = TipoAtendimento.Delivery;
    public decimal ValorCashbackUsado { get; set; } = 0m;
    public List<NovoItemPedidoDto> Itens { get; set; } = new();

    // FASE 6: momento do pagamento
    public MomentoPagamento MomentoPagamento { get; set; } = MomentoPagamento.NaEntrega;
    public MomentoPagamento? SegundoMomentoPagamento { get; set; }

    // FASE 7: Checkout Transparente Cartão (MP Bricks) — null quando não aplicável
    public string? CardToken { get; set; }
    public string? CardPaymentMethodId { get; set; }
    public int CardInstallments { get; set; } = 1;
    public string? CardEmailPagador { get; set; }
}

public class NovoItemPedidoDto
{
    public int ProdutoId { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public CategoriaEnum CategoriaId { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public string Observacao { get; set; } = string.Empty;
}
