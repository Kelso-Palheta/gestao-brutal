using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(6)]
public class V006__CreateProdutos : Migration
{
    public override void Up()
    {
        Create.Table("produtos")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("nome").AsString(100).NotNullable()
            .WithColumn("descricao").AsString(500).Nullable()
            .WithColumn("categoria_id").AsInt32().NotNullable()
            .WithColumn("preco_base").AsDecimal(19, 5).NotNullable()
            .WithColumn("imagem_url").AsString(255).Nullable()
            .WithColumn("ativo").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("ordem").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("complementos_permitidos").AsString(500).NotNullable().WithDefaultValue("")
            .WithColumn("estoque_atual").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("estoque_minimo").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down() => Delete.Table("produtos");
}
