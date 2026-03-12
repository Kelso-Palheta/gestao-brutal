using System;
using System.Collections.Generic;
using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Shared.DTOs;

public class PedidoDetalheDto
{
    public int Id { get; set; }
    public string NomeCliente { get; set; } = string.Empty;
    public string TelefoneCliente { get; set; } = string.Empty;
    public string EnderecoEntrega { get; set; } = string.Empty;
    public string NomeBairro { get; set; } = string.Empty;
    public DateTime DataHoraPedido { get; set; }
    public StatusPedido Status { get; set; }
    public MetodoPagamento MetodoPagamento { get; set; }
    public StatusPagamento StatusPagamento { get; set; }
    public TipoAtendimento TipoAtendimento { get; set; }
    public string LinkPagamento { get; set; } = string.Empty;
    public decimal? TrocoPara { get; set; }
    public decimal ValorTotal { get; set; }
    public List<ItemPedidoDetalheDto> Itens { get; set; } = new();
}

public class ItemPedidoDetalheDto
{
    public int Id { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public string Observacao { get; set; } = string.Empty;
}
