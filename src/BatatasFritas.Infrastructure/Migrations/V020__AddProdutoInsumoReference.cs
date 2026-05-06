using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260506003)]
public class V020__AddProdutoInsumoReference : Migration
{
    public override void Up()
    {
        Alter.Table("produtos")
            .AddColumn("insumo_id").AsInt32().Nullable()
            .ForeignKey("fk_produtos_insumo_id").References("insumos")(id);
    }

    public override void Down()
    {
        Delete.ForeignKey("fk_produtos_insumo_id").OnTable("produtos");
        Delete.Column("insumo_id").FromTable("produtos");
    }
}
