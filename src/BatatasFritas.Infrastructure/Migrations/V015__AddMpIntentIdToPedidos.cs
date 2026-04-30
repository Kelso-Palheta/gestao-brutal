using FluentMigrator;

namespace BatatasFritas.Infrastructure.Migrations;

/// <summary>
/// FASE 8 — Armazena o MP Point Intent ID em pedidos do totem
/// para rastreamento, cancelamento e estorno via painel MP.
/// </summary>
[Migration(20260429001)]
public class V015__AddMpIntentIdToPedidos : Migration
{
    public override void Up()
    {
        Alter.Table("pedidos")
            .AddColumn("mp_intent_id").AsString(200).Nullable();
    }

    public override void Down()
    {
        Delete.Column("mp_intent_id").FromTable("pedidos");
    }
}
