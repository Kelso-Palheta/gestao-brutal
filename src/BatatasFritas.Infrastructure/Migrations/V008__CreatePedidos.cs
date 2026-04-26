using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(8)]
public class V008__CreatePedidos : Migration
{
    public override void Up()
    {
        Create.Table("pedidos")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("nome_cliente").AsString(100).NotNullable()
            .WithColumn("telefone_cliente").AsString(20).NotNullable()
            .WithColumn("endereco_entrega").AsString(200).Nullable()
            .WithColumn("bairro_id").AsInt32().Nullable().ForeignKey("fk_pedidos_bairros", "bairros", "id")
            .WithColumn("data_hora").AsDateTime().NotNullable()
            .WithColumn("status").AsInt32().NotNullable()
            .WithColumn("metodo_pagamento").AsInt32().NotNullable()
            .WithColumn("segundo_metodo_pagamento").AsInt32().Nullable()
            .WithColumn("valor_segundo_pagamento").AsDecimal(19, 5).Nullable()
            .WithColumn("valor_cashback_usado").AsDecimal(19, 5).NotNullable().WithDefaultValue(0)
            .WithColumn("troco_para").AsDecimal(19, 5).Nullable()
            .WithColumn("observacao").AsString(500).Nullable()
            .WithColumn("link_pagamento").AsString(1000).Nullable()
            .WithColumn("status_pagamento").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("tipo_atendimento").AsInt32().NotNullable().WithDefaultValue(1);
    }

    public override void Down() => Delete.Table("pedidos");
}
