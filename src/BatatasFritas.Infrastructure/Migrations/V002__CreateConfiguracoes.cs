using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(2)]
public class V002__CreateConfiguracoes : Migration
{
    public override void Up()
    {
        Create.Table("configuracoes")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("chave").AsString(100).NotNullable().Unique()
            .WithColumn("valor").AsString(500).NotNullable();
    }

    public override void Down() => Delete.Table("configuracoes");
}
