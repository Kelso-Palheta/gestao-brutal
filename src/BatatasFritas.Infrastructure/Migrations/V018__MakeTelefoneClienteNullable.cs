using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260506001)]
public class V018__MakeTelefoneClienteNullable : Migration
{
    public override void Up()
    {
        Execute.Sql("ALTER TABLE pedidos ALTER COLUMN telefone_cliente DROP NOT NULL;");
    }

    public override void Down()
    {
        Execute.Sql("ALTER TABLE pedidos ALTER COLUMN telefone_cliente SET NOT NULL;");
    }
}
