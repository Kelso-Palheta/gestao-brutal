using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class InsumoMap : ClassMap<Insumo>
{
    public InsumoMap()
    {
        Table("insumos");
        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        Map(x => x.Nome).Not.Nullable().Length(100).Column("nome");
        Map(x => x.Unidade).Not.Nullable().Length(10).Column("unidade");
        Map(x => x.EstoqueAtual).Not.Nullable().Column("estoque_atual");
        Map(x => x.EstoqueMinimo).Not.Nullable().Column("estoque_minimo");
        Map(x => x.CustoPorUnidade).Not.Nullable().Column("custo_por_unidade");
        Map(x => x.Ativo).Not.Nullable().Column("ativo").Default("true");
    }
}
