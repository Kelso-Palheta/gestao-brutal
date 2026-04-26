using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(9)]
public class V009__CreateItensPedido : Migration
{
    public override void Up()
    {
        Create.Table("itens_pedido")
            .WithColumn("id").AsInt32().PrimaryKey().Identity()
            .WithColumn("pedido_id").AsInt32().NotNullable().ForeignKey("fk_itens_pedido_pedidos", "pedidos", "id")
            .WithColumn("produto_id").AsInt32().NotNullable().ForeignKey("fk_itens_pedido_produtos", "produtos", "id")
            .WithColumn("quantidade").AsInt32().NotNullable()
            .WithColumn("preco_unitario").AsDecimal(19, 5).NotNullable()
            .WithColumn("observacao").AsString(200).Nullable();
    }

    public override void Down() => Delete.Table("itens_pedido");
}
