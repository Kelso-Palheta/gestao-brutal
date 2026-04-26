using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(1)]
public class V001__CreateBairros : Migration
{
    public override void Up()
    {
        Create.Table("bairros")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("nome").AsString(100).NotNullable()
            .WithColumn("taxa_entrega").AsDecimal(19, 5).NotNullable()
            .WithColumn("ordem_exibicao").AsInt32().NotNullable().WithDefaultValue(0);
    }

    public override void Down() => Delete.Table("bairros");
}
