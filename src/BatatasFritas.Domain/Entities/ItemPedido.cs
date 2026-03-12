namespace BatatasFritas.Domain.Entities;

public class ItemPedido : EntityBase
{
    public virtual Pedido Pedido { get; protected set; } = null!;
    public virtual Produto Produto { get; protected set; } = null!;
    public virtual int Quantidade { get; protected set; }
    public virtual decimal PrecoUnitario { get; protected set; }
    public virtual string Observacao { get; protected set; } = string.Empty;

    protected ItemPedido() { } // NHibernate

    public ItemPedido(Pedido pedido, Produto produto, int quantidade, decimal precoUnitario, string observacao = "")
    {
        Pedido = pedido;
        Produto = produto;
        Quantidade = quantidade;
        PrecoUnitario = precoUnitario;
        Observacao = observacao;
    }
}
