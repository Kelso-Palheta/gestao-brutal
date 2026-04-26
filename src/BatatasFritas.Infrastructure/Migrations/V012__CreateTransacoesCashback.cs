using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(12)]
public class V012__CreateTransacoesCashback : Migration
{
    public override void Up()
    {
        Create.Table("transacoes_cashback")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("valor").AsDecimal(10, 2).NotNullable()
            .WithColumn("tipo").AsInt32().NotNullable()
            .WithColumn("motivo").AsString(255).NotNullable()
            .WithColumn("pedidoreferenciaid").AsInt32().Nullable()
            .WithColumn("datahora").AsDateTime().NotNullable()
            .WithColumn("carteiraid").AsInt32().NotNullable().ForeignKey("fk_transacoes_carteiras", "carteiras_cashback", "id");
    }

    public override void Down() => Delete.Table("transacoes_cashback");
}
