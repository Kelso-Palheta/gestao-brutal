using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(7)]
public class V007__CreateDespesas : Migration
{
    public override void Up()
    {
        if (!Schema.Table("despesas").Exists())
        {
            Create.Table("despesas")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("descricao").AsString(200).NotNullable()
                .WithColumn("valor").AsDecimal(19, 5).NotNullable()
                .WithColumn("dataregistro").AsDateTime().NotNullable()
                .WithColumn("categoria").AsString(50).NotNullable()
                .WithColumn("observacao").AsString(500).Nullable();
        }
    }

    public override void Down() => Delete.Table("despesas");
}
