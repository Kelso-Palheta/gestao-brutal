using BatatasFritas.Domain.Entities;
using FluentNHibernate.Mapping;
using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Infrastructure.Mappings;

public class PedidoMap : ClassMap<Pedido>
{
    public PedidoMap()
    {
        Table("pedidos");

        Id(x => x.Id).GeneratedBy.Identity().Column("id");
        Map(x => x.NomeCliente).Not.Nullable().Length(100).Column("nome_cliente");
        Map(x => x.TelefoneCliente).Not.Nullable().Length(20).Column("telefone_cliente");
        Map(x => x.EnderecoEntrega).Length(200).Column("endereco_entrega");
        References(x => x.BairroEntrega).Column("bairro_id").Nullable();

        Map(x => x.DataHoraPedido).Not.Nullable().Column("data_hora");
        Map(x => x.Status).CustomType<StatusPedido>().Not.Nullable().Column("status");
        Map(x => x.MetodoPagamento).CustomType<MetodoPagamento>().Not.Nullable().Column("metodo_pagamento");
        Map(x => x.SegundoMetodoPagamento).CustomType<MetodoPagamento>().Column("segundo_metodo_pagamento").Nullable();
        Map(x => x.ValorSegundoPagamento).Column("valor_segundo_pagamento").Nullable();
        Map(x => x.ValorCashbackUsado).Column("valor_cashback_usado").Not.Nullable().Default("0");
        Map(x => x.TrocoPara).Column("troco_para").Nullable();
        Map(x => x.Observacao).Column("observacao").Nullable().Length(500);

        Map(x => x.LinkPagamento).Column("link_pagamento").Nullable().Length(1000);
        Map(x => x.StatusPagamento).CustomType<StatusPagamento>().Not.Nullable().Column("status_pagamento").Default("1");
        Map(x => x.TipoAtendimento).CustomType<TipoAtendimento>().Not.Nullable().Column("tipo_atendimento").Default("1");

        // FASE 5: Pix Direto
        Map(x => x.QrCodeBase64).Column("qr_code_base64").Nullable().CustomSqlType("text");
        Map(x => x.QrCodeTexto).Column("qr_code_texto").Nullable().CustomSqlType("text");
        Map(x => x.MpPagamentoId).Column("mp_pagamento_id").Nullable();

        // FASE 6: Momento do pagamento
        Map(x => x.MomentoPagamento).CustomType<MomentoPagamento>().Not.Nullable().Column("momento_pagamento").Default("2");
        Map(x => x.SegundoMomentoPagamento).CustomType<MomentoPagamento>().Column("segundo_momento_pagamento").Nullable();

        // FASE 8: MP Point Intent ID (totem — rastreio/estorno)
        Map(x => x.MpIntentId).Column("mp_intent_id").Nullable().Length(200);

        // FASE 3.5: E2E ID do PIX para prevenção de reuso de comprovante
        Map(x => x.ComprovantePix).Column("comprovante_pix").Nullable().Length(100);

        HasMany(x => x.Itens)
            .KeyColumn("pedido_id")
            .Inverse()
            .Cascade.AllDeleteOrphan();
    }
}
