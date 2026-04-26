using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(10)]
public class V010__CreateItensReceita : Migration
{
    public override void Up()
    {
        Create.Table("itens_receita")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("produto_id").AsInt32().NotNullable().ForeignKey("fk_itens_receita_produtos", "produtos", "id")
            .WithColumn("insumo_id").AsInt32().NotNullable().ForeignKey("fk_itens_receita_insumos", "insumos", "id")
            .WithColumn("quantidade_por_unidade").AsDecimal(19, 5).NotNullable();
    }

    public override void Down() => Delete.Table("itens_receita");
}
