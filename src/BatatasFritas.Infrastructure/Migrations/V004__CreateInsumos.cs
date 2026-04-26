using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(4)]
public class V004__CreateInsumos : Migration
{
    public override void Up()
    {
        Create.Table("insumos")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("nome").AsString(100).NotNullable()
            .WithColumn("unidade").AsString(10).NotNullable()
            .WithColumn("estoque_atual").AsDecimal(19, 5).NotNullable()
            .WithColumn("estoque_minimo").AsDecimal(19, 5).NotNullable()
            .WithColumn("custo_por_unidade").AsDecimal(19, 5).NotNullable()
            .WithColumn("ativo").AsBoolean().NotNullable().WithDefaultValue(true);
    }

    public override void Down() => Delete.Table("insumos");
}
