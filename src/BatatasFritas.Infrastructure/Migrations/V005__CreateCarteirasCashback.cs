using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(5)]
public class V005__CreateCarteirasCashback : Migration
{
    public override void Up()
    {
        Create.Table("carteiras_cashback")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("telefone").AsString(20).NotNullable().Unique()
            .WithColumn("nomecliente").AsString(100).Nullable()
            .WithColumn("saldoatual").AsDecimal(10, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("criadoem").AsDateTime().NotNullable();
    }

    public override void Down() => Delete.Table("carteiras_cashback");
}
