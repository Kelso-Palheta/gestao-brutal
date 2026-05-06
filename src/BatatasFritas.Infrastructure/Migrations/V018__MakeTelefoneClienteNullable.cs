using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260506001)]
public class V018__MakeTelefoneClienteNullable : Migration
{
    public override void Up()
    {
        // PostgreSQL only: SQLite doesn't support ALTER COLUMN; column already nullable via PedidoMap.cs
        Execute.Sql("ALTER TABLE pedidos ALTER COLUMN telefone_cliente DROP NOT NULL;");
    }

    public override void Down()
    {
        Execute.Sql("ALTER TABLE pedidos ALTER COLUMN telefone_cliente SET NOT NULL;");
    }
}
