using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(13)]
public class V013__AddPixQrCodeToPedidos : Migration
{
    public override void Up()
    {
        if (!Schema.Table("pedidos").Column("qr_code_base64").Exists())
        {
            Alter.Table("pedidos")
                .AddColumn("qr_code_base64").AsCustom("text").Nullable();
        }

        if (!Schema.Table("pedidos").Column("qr_code_texto").Exists())
        {
            Alter.Table("pedidos")
                .AddColumn("qr_code_texto").AsCustom("text").Nullable();
        }

        if (!Schema.Table("pedidos").Column("mp_pagamento_id").Exists())
        {
            Alter.Table("pedidos")
                .AddColumn("mp_pagamento_id").AsInt64().Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("pedidos").Column("qr_code_base64").Exists())
            Delete.Column("qr_code_base64").FromTable("pedidos");
        if (Schema.Table("pedidos").Column("qr_code_texto").Exists())
            Delete.Column("qr_code_texto").FromTable("pedidos");
        if (Schema.Table("pedidos").Column("mp_pagamento_id").Exists())
            Delete.Column("mp_pagamento_id").FromTable("pedidos");
    }
}
