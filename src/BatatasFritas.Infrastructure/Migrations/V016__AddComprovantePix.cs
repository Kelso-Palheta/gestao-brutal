using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260501001)]
public class V016__AddComprovantePix : Migration
{
    public override void Up()
    {
        Alter.Table("pedidos")
            .AddColumn("comprovante_pix").AsString(100).Nullable();

        // PostgreSQL: NULLs são distintos em índices UNIQUE (cada NULL é único)
        // → múltiplos pedidos sem comprovante permitidos; duplicata de E2E ID bloqueada
        Execute.Sql(
            "CREATE UNIQUE INDEX ix_pedidos_comprovante_pix ON pedidos(comprovante_pix) WHERE comprovante_pix IS NOT NULL;");
    }

    public override void Down()
    {
        Execute.Sql("DROP INDEX IF EXISTS ix_pedidos_comprovante_pix;");
        Delete.Column("comprovante_pix").FromTable("pedidos");
    }
}
