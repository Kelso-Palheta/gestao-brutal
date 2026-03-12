using System;
using System.Collections.Generic;
using System.Linq;
using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Domain.Entities;

public class Pedido : EntityBase
{
    public virtual string NomeCliente { get; protected set; } = string.Empty;
    public virtual string TelefoneCliente { get; protected set; } = string.Empty;
    public virtual string EnderecoEntrega { get; protected set; } = string.Empty;
    public virtual Bairro? BairroEntrega { get; protected set; }
    
    public virtual decimal TaxaEntrega => BairroEntrega?.TaxaEntrega ?? 0m;
    public virtual decimal ValorTotalItens => Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
    public virtual decimal ValorCashbackUsado { get; protected set; } = 0m;
    public virtual decimal ValorTotal => Math.Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado);

    public virtual DateTime DataHoraPedido { get; protected set; }
    public virtual StatusPedido Status { get; protected set; }
    public virtual MetodoPagamento MetodoPagamento { get; protected set; }
    public virtual decimal? TrocoPara { get; protected set; }

    // -- Novos campos Módulo I e F --
    public virtual string LinkPagamento { get; protected set; } = string.Empty;
    public virtual StatusPagamento StatusPagamento { get; set; } = StatusPagamento.Pendente;
    public virtual TipoAtendimento TipoAtendimento { get; protected set; } = TipoAtendimento.Delivery;

    public virtual IList<ItemPedido> Itens { get; protected set; } = new List<ItemPedido>();

    /// <summary>
    /// Campo livre — usado para registrar o motivo de cancelamento ou anotações do operador KDS.
    /// </summary>
    public virtual string Observacao { get; set; } = string.Empty;

    protected Pedido() { } // NHibernate

    public Pedido(string nomeCliente, string telefoneCliente, string enderecoEntrega, Bairro? bairroEntrega, MetodoPagamento metodoPagamento, decimal? trocoPara = null, TipoAtendimento tipoAtendimento = TipoAtendimento.Delivery, decimal valorCashbackUsado = 0m)
    {
        NomeCliente = nomeCliente;
        TelefoneCliente = telefoneCliente;
        EnderecoEntrega = enderecoEntrega;
        BairroEntrega = bairroEntrega;
        MetodoPagamento = metodoPagamento;
        TrocoPara = trocoPara;
        TipoAtendimento = tipoAtendimento;
        ValorCashbackUsado = valorCashbackUsado;
        DataHoraPedido = DateTime.UtcNow;
        Status = StatusPedido.Recebido;
        StatusPagamento = StatusPagamento.Pendente;
        
        // Se for dinheiro presencial, nós aceitamos que a pessoa vá pagar ao entregador/balção (mas marcamos como 'Presencial' depois ou iniciamos com 'Pendente')
    }

    public virtual void SetLinkPagamento(string link)
    {
        LinkPagamento = link;
    }


    public virtual void AdicionarItem(Produto produto, int quantidade, decimal precoUnitario, string observacao = "")
    {
        var item = new ItemPedido(this, produto, quantidade, precoUnitario, observacao);
        Itens.Add(item);
    }

    public virtual void AlterarStatus(StatusPedido novoStatus)
    {
        Status = novoStatus;
    }
}
