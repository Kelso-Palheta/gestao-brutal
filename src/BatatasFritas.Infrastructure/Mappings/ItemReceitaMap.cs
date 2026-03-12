using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class ItemReceitaMap : ClassMap<ItemReceita>
{
    public ItemReceitaMap()
    {
        Table("itens_receita");
        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        References(x => x.Produto).Column("produto_id").Not.Nullable();
        References(x => x.Insumo).Column("insumo_id").Not.Nullable();
        Map(x => x.QuantidadePorUnidade).Not.Nullable().Column("quantidade_por_unidade");
    }
}
