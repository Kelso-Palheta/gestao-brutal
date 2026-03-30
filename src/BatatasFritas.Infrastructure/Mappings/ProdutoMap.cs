using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;
using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Infrastructure.Mappings;

public class ProdutoMap : ClassMap<Produto>
{
    public ProdutoMap()
    {
        Table("produtos");

        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        Map(x => x.Nome).Not.Nullable().Length(100).Column("nome");
        Map(x => x.Descricao).Length(500).Column("descricao");
        Map(x => x.CategoriaId).CustomType<CategoriaEnum>().Not.Nullable().Column("categoria_id");
        Map(x => x.PrecoBase).Not.Nullable().Column("preco_base");
        Map(x => x.ImagemUrl).Length(255).Column("imagem_url");
        Map(x => x.Ativo).Not.Nullable().Column("ativo").Default("true");
        Map(x => x.Ordem).Not.Nullable().Column("ordem").Default("0");
        Map(x => x.ComplementosPermitidos).Nullable().Length(500).Column("complementos_permitidos").Default("''");
    }
}
