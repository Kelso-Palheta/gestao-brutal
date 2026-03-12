using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class ItemPedidoMap : ClassMap<ItemPedido>
{
    public ItemPedidoMap()
    {
        Table("itens_pedido");

        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        References(x => x.Pedido).Column("pedido_id").Not.Nullable();
        References(x => x.Produto).Column("produto_id").Not.Nullable();
        Map(x => x.Quantidade).Not.Nullable().Column("quantidade");
        Map(x => x.PrecoUnitario).Not.Nullable().Column("preco_unitario");
        Map(x => x.Observacao).Length(200).Column("observacao");
    }
}
