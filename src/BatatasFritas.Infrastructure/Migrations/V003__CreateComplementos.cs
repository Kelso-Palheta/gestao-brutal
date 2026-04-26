using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(3)]
public class V003__CreateComplementos : Migration
{
    public override void Up()
    {
        if (!Schema.Table("complementos").Exists())
        {
            Create.Table("complementos")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("nome").AsString(100).NotNullable()
                .WithColumn("preco").AsDecimal(19, 5).NotNullable()
                .WithColumn("categoriaalvo").AsString(30).NotNullable()
                .WithColumn("tipoacao").AsString(30).NotNullable()
                .WithColumn("ativo").AsBoolean().NotNullable();
        }
    }

    public override void Down() => Delete.Table("complementos");
}
