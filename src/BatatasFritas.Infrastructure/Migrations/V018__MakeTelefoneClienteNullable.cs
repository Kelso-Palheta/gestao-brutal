using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260506001)]
public class V018__MakeTelefoneClienteNullable : Migration
{
    public override void Up()
    {
        Alter.Table("pedidos")
            .AlterColumn("telefone_cliente")
            .AsString(20)
            .Nullable();
    }

    public override void Down()
    {
        Alter.Table("pedidos")
            .AlterColumn("telefone_cliente")
            .AsString(20)
            .NotNullable();
    }
}
