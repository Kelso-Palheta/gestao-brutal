using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(20260506002)]
public class V019__AddInsumoVisibilityFlags : Migration
{
    public override void Up()
    {
        Alter.Table("insumos")
            .AddColumn("mostrar_no_cardapio").AsBoolean().WithDefaultValue(false).NotNullable()
            .AddColumn("auto_desativar_ao_zerar").AsBoolean().WithDefaultValue(false).NotNullable();
    }

    public override void Down()
    {
        Delete.Column("mostrar_no_cardapio").FromTable("insumos");
        Delete.Column("auto_desativar_ao_zerar").FromTable("insumos");
    }
}
