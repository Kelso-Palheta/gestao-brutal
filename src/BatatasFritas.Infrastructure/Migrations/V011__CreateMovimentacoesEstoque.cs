using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

[Migration(11)]
public class V011__CreateMovimentacoesEstoque : Migration
{
    public override void Up()
    {
        if (!Schema.Table("movimentacoes_estoque").Exists())
        {
            Create.Table("movimentacoes_estoque")
                .WithColumn("id").AsInt32().PrimaryKey().Identity()
                .WithColumn("insumo_id").AsInt32().NotNullable().ForeignKey("fk_movimentacoes_insumos", "insumos", "id")
                .WithColumn("tipo").AsInt32().NotNullable()
                .WithColumn("quantidade").AsDecimal(19, 5).NotNullable()
                .WithColumn("valor_unitario").AsDecimal(19, 5).NotNullable()
                .WithColumn("data_movimentacao").AsDateTime().NotNullable()
                .WithColumn("motivo").AsString(300).Nullable()
                .WithColumn("fornecedor").AsString(150).Nullable()
                .WithColumn("numero_nf").AsString(50).Nullable();
        }
    }

    public override void Down() => Delete.Table("movimentacoes_estoque");
}
