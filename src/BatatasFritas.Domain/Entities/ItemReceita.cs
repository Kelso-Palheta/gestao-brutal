namespace BatatasFritas.Domain.Entities;

/// <summary>
/// Define quanto de um insumo é consumido quando um produto é vendido.
/// Ex: "Brutal de Respeito" consome 0.4kg de Batata Rústica e 0.05kg de Cheddar.
/// </summary>
public class ItemReceita : EntityBase
{
    public virtual Produto Produto { get; protected set; } = null!;
    public virtual Insumo Insumo { get; protected set; } = null!;
    /// <summary>Quantidade do insumo consumida por 1 unidade do produto.</summary>
    public virtual decimal QuantidadePorUnidade { get; protected set; }

    protected ItemReceita() { } // NHibernate

    public ItemReceita(Produto produto, Insumo insumo, decimal quantidadePorUnidade)
    {
        Produto = produto;
        Insumo = insumo;
        QuantidadePorUnidade = quantidadePorUnidade;
    }

    public virtual void AtualizarQuantidade(decimal novaQuantidade)
        => QuantidadePorUnidade = novaQuantidade;
}
