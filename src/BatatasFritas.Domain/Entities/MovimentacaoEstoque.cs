namespace BatatasFritas.Domain.Entities;

public enum TipoMovimentacao { Entrada = 1, Saida = 2, Ajuste = 3 }

/// <summary>Registro de movimentação de estoque (entrada de compra, saída por venda, ajuste manual).</summary>
public class MovimentacaoEstoque : EntityBase
{
    public virtual Insumo Insumo { get; protected set; } = null!;
    public virtual TipoMovimentacao Tipo { get; protected set; }
    public virtual decimal Quantidade { get; protected set; }
    public virtual decimal ValorUnitario { get; protected set; }  // custo por unidade neste lote
    public virtual decimal ValorTotal => Quantidade * ValorUnitario;
    public virtual DateTime DataMovimentacao { get; protected set; } = DateTime.UtcNow;
    public virtual string Motivo { get; protected set; } = string.Empty;
    public virtual string Fornecedor { get; protected set; } = string.Empty;
    public virtual string NumeroNF { get; protected set; } = string.Empty;

    protected MovimentacaoEstoque() { } // NHibernate

    public MovimentacaoEstoque(
        Insumo insumo,
        TipoMovimentacao tipo,
        decimal quantidade,
        decimal valorUnitario,
        string motivo,
        string fornecedor = "",
        string numeroNF = "")
    {
        Insumo = insumo;
        Tipo = tipo;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
        Motivo = motivo;
        Fornecedor = fornecedor;
        NumeroNF = numeroNF;
        DataMovimentacao = DateTime.UtcNow;

        // Atualiza estoque
        if (tipo == TipoMovimentacao.Entrada)
            insumo.AjustarEstoque(quantidade);
        else if (tipo == TipoMovimentacao.Saida)
            insumo.AjustarEstoque(-quantidade);
        else // Ajuste: quantidade pode ser positiva ou negativa
            insumo.AjustarEstoque(quantidade);
    }
}
