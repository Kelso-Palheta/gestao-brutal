using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class DespesaMap : ClassMap<Despesa>
{
    public DespesaMap()
    {
        Table("despesas");
        Id(x => x.Id).GeneratedBy.Identity();
        Map(x => x.Descricao).Not.Nullable().Length(200);
        Map(x => x.Valor).Not.Nullable();
        Map(x => x.DataRegistro).Not.Nullable();
        Map(x => x.Categoria).Not.Nullable().Length(50);
    }
}
