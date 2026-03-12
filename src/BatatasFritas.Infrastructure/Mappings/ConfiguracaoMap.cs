using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class ConfiguracaoMap : ClassMap<Configuracao>
{
    public ConfiguracaoMap()
    {
        Table("configuracoes");

        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        Map(x => x.Chave).Not.Nullable().Unique().Length(100).Column("chave");
        Map(x => x.Valor).Not.Nullable().Length(500).Column("valor");
    }
}
