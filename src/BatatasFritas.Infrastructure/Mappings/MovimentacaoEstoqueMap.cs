using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class MovimentacaoEstoqueMap : ClassMap<MovimentacaoEstoque>
{
    public MovimentacaoEstoqueMap()
    {
        Table("movimentacoes_estoque");
        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        References(x => x.Insumo).Column("insumo_id").Not.Nullable();
        Map(x => x.Tipo).CustomType<TipoMovimentacao>().Not.Nullable().Column("tipo");
        Map(x => x.Quantidade).Not.Nullable().Column("quantidade");
        Map(x => x.ValorUnitario).Not.Nullable().Column("valor_unitario");
        Map(x => x.DataMovimentacao).Not.Nullable().Column("data_movimentacao");
        Map(x => x.Motivo).Length(300).Column("motivo");
        Map(x => x.Fornecedor).Length(150).Column("fornecedor");
        Map(x => x.NumeroNF).Length(50).Column("numero_nf");
    }
}
