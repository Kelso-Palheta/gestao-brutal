using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260501002)]
public class V017__AddMotivoEstorno : Migration
{
    public override void Up()
    {
        Alter.Table("pedidos")
            .AddColumn("motivo_estorno").AsString(500).Nullable();
    }

    public override void Down()
    {
        Delete.Column("motivo_estorno").FromTable("pedidos");
    }
}
