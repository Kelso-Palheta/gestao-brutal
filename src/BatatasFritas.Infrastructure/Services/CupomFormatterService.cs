using System;
using System.Text;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Infrastructure.Services;

/// <summary>
/// Gera texto formatado em colunas fixas (40 chars) compatível com impressoras térmicas ESC/POS
/// e com o protocolo do PrintAgent (Bematech MP-4000 TH).
/// </summary>
public static class CupomFormatterService
{
    private const int Largura = 40;

    public static string Formatar(Pedido pedido)
    {
        var sb = new StringBuilder();

        sb.AppendLine(Centralizar("================================"));
        sb.AppendLine(Centralizar("     BATATAS FRITAS DELIVERY    "));
        sb.AppendLine(Centralizar("================================"));
        sb.AppendLine();

        var hora = pedido.DataHoraPedido.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
        sb.AppendLine(Coluna($"Pedido #{pedido.Id}", hora));
        sb.AppendLine(Separador());

        sb.AppendLine($"Cliente : {pedido.NomeCliente}");
        if (!string.IsNullOrWhiteSpace(pedido.TelefoneCliente))
            sb.AppendLine($"Tel     : {pedido.TelefoneCliente}");

        var local = pedido.TipoAtendimento == TipoAtendimento.Balcao || pedido.TipoAtendimento == TipoAtendimento.Totem
            ? "Retirada no local"
            : pedido.BairroEntrega?.Nome ?? "Entrega";
        sb.AppendLine($"Local   : {local}");

        if (!string.IsNullOrWhiteSpace(pedido.EnderecoEntrega))
            sb.AppendLine($"Endereço: {Truncar(pedido.EnderecoEntrega, Largura - 10)}");

        sb.AppendLine(Separador());
        sb.AppendLine("ITENS:");
        sb.AppendLine(Separador());

        foreach (var item in pedido.Itens)
        {
            var nomeLimitado = Truncar(item.Produto.Nome, 22);
            var subtotal = (item.PrecoUnitario * item.Quantidade).ToString("F2");
            sb.AppendLine(Coluna($"{item.Quantidade}x {nomeLimitado}", $"R$ {subtotal}"));
            if (!string.IsNullOrWhiteSpace(item.Observacao))
                sb.AppendLine($"   Obs: {item.Observacao}");
        }

        sb.AppendLine(Separador());

        if (pedido.BairroEntrega?.TaxaEntrega > 0)
            sb.AppendLine(Coluna("Taxa de entrega:", $"R$ {pedido.BairroEntrega.TaxaEntrega:F2}"));

        if (pedido.ValorCashbackUsado > 0)
            sb.AppendLine(Coluna("Cashback usado:", $"-R$ {pedido.ValorCashbackUsado:F2}"));

        sb.AppendLine(Coluna("TOTAL:", $"R$ {pedido.ValorTotal:F2}"));
        sb.AppendLine(Separador());

        // ── Pagamento ────────────────────────────────────────────────
        sb.AppendLine("PAGAMENTO:");
        FormatarPagamento(sb, pedido);

        if (pedido.TrocoPara.HasValue && pedido.TrocoPara > 0)
            sb.AppendLine(Coluna("Troco para:", $"R$ {pedido.TrocoPara:F2}"));

        sb.AppendLine(Separador());
        sb.AppendLine(Centralizar("Obrigado pela preferência!"));
        sb.AppendLine(Centralizar($"BatatasFreitas — {DateTime.Now:HH:mm}"));
        sb.AppendLine();
        sb.AppendLine(); // espaço corte papel

        return sb.ToString();
    }

    // ── helpers ───────────────────────────────────────────────────────────

    private static void FormatarPagamento(StringBuilder sb, Pedido pedido)
    {
        var metodo1 = DescricaoMetodo(pedido.MetodoPagamento);
        var status1 = pedido.StatusPagamento == StatusPagamento.Aprovado
                   || pedido.StatusPagamento == StatusPagamento.PagamentoParcial
                   || pedido.StatusPagamento == StatusPagamento.Presencial
            ? "APROVADO"
            : pedido.MomentoPagamento == MomentoPagamento.NaEntrega ? "NA ENTREGA" : "AGUARDANDO";

        if (pedido.SegundoMetodoPagamento.HasValue && pedido.ValorSegundoPagamento.HasValue)
        {
            // Pagamento split
            var valor1 = pedido.ValorTotal - pedido.ValorSegundoPagamento.Value;
            sb.AppendLine(Coluna($"1) {metodo1} ({status1}):", $"R$ {valor1:F2}"));

            var metodo2 = DescricaoMetodo(pedido.SegundoMetodoPagamento.Value);
            var status2 = pedido.StatusPagamento == StatusPagamento.Aprovado ? "APROVADO" : "NA ENTREGA";
            sb.AppendLine(Coluna($"2) {metodo2} ({status2}):", $"R$ {pedido.ValorSegundoPagamento.Value:F2}"));
        }
        else
        {
            sb.AppendLine(Coluna($"{metodo1}:", status1));
        }
    }

    private static string DescricaoMetodo(MetodoPagamento m) => m switch
    {
        MetodoPagamento.Pix      => "Pix",
        MetodoPagamento.Cartao   => "Cartão",
        MetodoPagamento.Dinheiro => "Dinheiro",
        _ => m.ToString()
    };

    private static string Separador() => new string('-', Largura);

    private static string Centralizar(string texto)
    {
        if (texto.Length >= Largura) return texto;
        var pad = (Largura - texto.Length) / 2;
        return texto.PadLeft(texto.Length + pad).PadRight(Largura);
    }

    private static string Coluna(string esq, string dir)
    {
        var espacos = Largura - esq.Length - dir.Length;
        if (espacos < 1) espacos = 1;
        return esq + new string(' ', espacos) + dir;
    }

    private static string Truncar(string s, int max)
        => s.Length <= max ? s : s[..max];
}
