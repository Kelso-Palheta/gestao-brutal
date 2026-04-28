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
    
    // Novo: Valor elegível para cashback (apenas produtos de categoria Batatas, Porções ou complementos pagos)
    public virtual decimal ValorElegivelCashback => Itens
        .Where(i => i.Produto.CategoriaId == CategoriaEnum.Batatas || 
                    i.Produto.CategoriaId == CategoriaEnum.Porcoes)
        .Sum(i => i.PrecoUnitario * i.Quantidade);

    public virtual decimal ValorCashbackUsado { get; protected set; } = 0m;
    public virtual decimal ValorTotal => Math.Max(0, ValorTotalItens + TaxaEntrega - ValorCashbackUsado);

    public virtual DateTime DataHoraPedido { get; protected set; }
    public virtual StatusPedido Status { get; protected set; }
    public virtual MetodoPagamento MetodoPagamento { get; protected set; }
    public virtual MetodoPagamento? SegundoMetodoPagamento { get; protected set; }
    public virtual decimal? TrocoPara { get; protected set; }
    public virtual decimal? ValorSegundoPagamento { get; protected set; }

    // -- Novos campos Módulo I e F --
    public virtual string LinkPagamento { get; protected set; } = string.Empty;
    public virtual StatusPagamento StatusPagamento { get; set; } = StatusPagamento.Pendente;
    public virtual TipoAtendimento TipoAtendimento { get; protected set; } = TipoAtendimento.Delivery;

    // -- FASE 5: Pix Direto (PaymentClient MP) --
    public virtual string? QrCodeBase64 { get; protected set; }
    public virtual string? QrCodeTexto { get; protected set; }
    public virtual long? MpPagamentoId { get; protected set; }

    // -- FASE 6: Momento do pagamento (Online | NaEntrega) --
    public virtual MomentoPagamento MomentoPagamento { get; protected set; } = MomentoPagamento.NaEntrega;
    public virtual MomentoPagamento? SegundoMomentoPagamento { get; protected set; }

    public virtual IList<ItemPedido> Itens { get; protected set; } = new List<ItemPedido>();

    /// <summary>
    /// Campo livre — usado para registrar o motivo de cancelamento ou anotações do operador KDS.
    /// </summary>
    public virtual string Observacao { get; set; } = string.Empty;

    protected Pedido() { } // NHibernate

    public Pedido(
        string nomeCliente,
        string telefoneCliente,
        string enderecoEntrega,
        Bairro? bairroEntrega,
        MetodoPagamento metodoPagamento,
        decimal? trocoPara = null,
        TipoAtendimento tipoAtendimento = TipoAtendimento.Delivery,
        decimal valorCashbackUsado = 0m,
        MetodoPagamento? segundoMetodoPagamento = null,
        decimal? valorSegundoPagamento = null,
        MomentoPagamento momentoPagamento = MomentoPagamento.NaEntrega,
        MomentoPagamento? segundoMomentoPagamento = null)
    {
        NomeCliente = nomeCliente;
        TelefoneCliente = telefoneCliente;
        EnderecoEntrega = enderecoEntrega;
        BairroEntrega = bairroEntrega;
        MetodoPagamento = metodoPagamento;
        SegundoMetodoPagamento = segundoMetodoPagamento;
        ValorSegundoPagamento = valorSegundoPagamento;
        MomentoPagamento = momentoPagamento;
        SegundoMomentoPagamento = segundoMomentoPagamento;
        TrocoPara = trocoPara;
        TipoAtendimento = tipoAtendimento;
        ValorCashbackUsado = valorCashbackUsado;
        DataHoraPedido = DateTime.UtcNow;
        Status = StatusPedido.Recebido;
        StatusPagamento = StatusPagamento.Pendente;
    }

    public virtual void SetLinkPagamento(string link)
    {
        LinkPagamento = link;
    }

    public virtual void SetPagamentoPix(string qrCodeBase64, string qrCodeTexto, long mpPagamentoId)
    {
        QrCodeBase64  = qrCodeBase64;
        QrCodeTexto   = qrCodeTexto;
        MpPagamentoId = mpPagamentoId;
        StatusPagamento = StatusPagamento.Pendente; // aguardando confirmação webhook
    }

    /// <summary>
    /// Chamado quando o 1º pagamento online é aprovado mas existe 2ª parte na entrega.
    /// </summary>
    public virtual void ConfirmarPagamentoParcial()
    {
        StatusPagamento = StatusPagamento.PagamentoParcial;
    }

    /// <summary>
    /// Chamado quando todos os pagamentos foram confirmados (aprovação completa).
    /// </summary>
    public virtual void ConfirmarPagamento()
    {
        StatusPagamento = StatusPagamento.Aprovado;
    }

    /// <summary>
    /// Chamado pelo operador KDS ao receber o pagamento da entrega (2ª parte).
    /// </summary>
    public virtual void ConfirmarPagamentoEntrega()
    {
        StatusPagamento = StatusPagamento.Aprovado;
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
