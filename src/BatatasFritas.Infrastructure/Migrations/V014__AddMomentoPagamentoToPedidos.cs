using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(14)]
public class V014__AddMomentoPagamentoToPedidos : Migration
{
    public override void Up()
    {
        // momento_pagamento: 1=Online, 2=NaEntrega (default=2 para pedidos existentes)
        if (!Schema.Table("pedidos").Column("momento_pagamento").Exists())
            Alter.Table("pedidos").AddColumn("momento_pagamento").AsInt32().NotNullable().WithDefaultValue(2);

        if (!Schema.Table("pedidos").Column("segundo_momento_pagamento").Exists())
            Alter.Table("pedidos").AddColumn("segundo_momento_pagamento").AsInt32().Nullable();
    }

    public override void Down()
    {
        if (Schema.Table("pedidos").Column("segundo_momento_pagamento").Exists())
            Delete.Column("segundo_momento_pagamento").FromTable("pedidos");

        if (Schema.Table("pedidos").Column("momento_pagamento").Exists())
            Delete.Column("momento_pagamento").FromTable("pedidos");
    }
}
