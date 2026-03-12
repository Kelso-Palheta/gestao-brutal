using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class TransacaoCashbackMap : ClassMap<TransacaoCashback>
{
    public TransacaoCashbackMap()
    {
        Table("transacoes_cashback");

        Id(x => x.Id).GeneratedBy.Identity();

        Map(x => x.Valor).Not.Nullable().Precision(10).Scale(2);
        Map(x => x.Tipo).CustomType<int>().Not.Nullable();
        Map(x => x.Motivo).Not.Nullable().Length(255);
        Map(x => x.PedidoReferenciaId).Nullable();
        Map(x => x.DataHora).Not.Nullable();

        // Relacionamento N:1 de volta para a carteira
        References(x => x.Carteira).Column("CarteiraId").Not.Nullable();
    }
}
