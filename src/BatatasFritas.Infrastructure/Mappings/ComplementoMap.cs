using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class ComplementoMap : ClassMap<Complemento>
{
    public ComplementoMap()
    {
        Table("complementos");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.Nome).Not.Nullable().Length(100);
        Map(x => x.Preco).Not.Nullable();
        Map(x => x.CategoriaAlvo).Not.Nullable().Length(30);
        Map(x => x.TipoAcao).Not.Nullable().Length(30);
        Map(x => x.Ativo).Not.Nullable();
    }
}
