using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260510001)]
public class V021__AddInsumoVinculadoToProdutos : Migration
{
    public override void Up()
    {
        Alter.Table("produtos")
            .AddColumn("insumo_vinculado_id").AsInt32().Nullable()
                .ForeignKey("fk_produto_insumo_vinculado", "insumos", "id").OnDelete(System.Data.Rule.SetNull)
            .AddColumn("quantidade_por_unidade").AsDecimal(10, 3).NotNullable().WithDefaultValue(1);
    }

    public override void Down()
    {
        Delete.ForeignKey("fk_produto_insumo_vinculado").OnTable("produtos");
        Delete.Column("insumo_vinculado_id").FromTable("produtos");
        Delete.Column("quantidade_por_unidade").FromTable("produtos");
    }
}
