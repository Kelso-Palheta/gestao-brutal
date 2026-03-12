using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;

namespace BatatasFritas.Infrastructure.Mappings;

public class CarteiraCashbackMap : ClassMap<CarteiraCashback>
{
    public CarteiraCashbackMap()
    {
        Table("carteiras_cashback");

        Id(x => x.Id).GeneratedBy.Identity();

        // O Telefone é usado como chave de busca natural (único)
        Map(x => x.Telefone).Not.Nullable().Length(20).Unique();
        Map(x => x.NomeCliente).Nullable().Length(100);
        
        // Precision 10, Scale 2
        Map(x => x.SaldoAtual).Not.Nullable().Precision(10).Scale(2).Default("0");
        Map(x => x.CriadoEm).Not.Nullable();

        // Relacionamento 1:N com as transações do cliente.
        // Cascade AllDeleteOrphan para que transações sejam salvas junto com a carteira.
        HasMany(x => x.Transacoes)
            .KeyColumn("CarteiraId")
            .Inverse()
            .Cascade.AllDeleteOrphan();
    }
}
