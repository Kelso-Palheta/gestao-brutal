using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class BairroMap : ClassMap<Bairro>
{
    public BairroMap()
    {
        Table("bairros");

        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        Map(x => x.Nome).Not.Nullable().Length(100).Column("nome");
        Map(x => x.TaxaEntrega).Not.Nullable().Column("taxa_entrega");
    }
}
