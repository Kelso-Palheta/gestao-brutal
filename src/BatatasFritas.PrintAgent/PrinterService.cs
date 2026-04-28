using System.IO.Ports;
using System.Text;
using BatatasFritas.PrintAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatatasFritas.PrintAgent;

public class ImpressoraOptions
{
    public string Porta    { get; set; } = "COM3";
    public int    BaudRate { get; set; } = 9600;
}

public class PrinterService
{
    private const int Largura = 40;

    private readonly ImpressoraOptions _opts;
    private readonly ILogger<PrinterService> _logger;

    public PrinterService(IOptions<ImpressoraOptions> opts, ILogger<PrinterService> logger)
    {
        _opts   = opts.Value;
        _logger = logger;
    }

    public void Imprimir(PedidoDetalheDto pedido)
    {
        try
        {
            var texto = FormatarCupom(pedido);
            var bytes = MontarEscPos(texto);

            using var port = new SerialPort(_opts.Porta, _opts.BaudRate);
            port.Open();
            port.Write(bytes, 0, bytes.Length);
            port.Close();

            _logger.LogInformation("[PRINT] Pedido #{Id} impresso em {Porta}", pedido.Id, _opts.Porta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PRINT] Falha ao imprimir pedido #{Id}", pedido.Id);
        }
    }

    // ── ESC/POS ──────────────────────────────────────────────────────────

    private static byte[] MontarEscPos(string texto)
    {
        var buf = new List<byte>();

        // ESC @ — inicializa impressora
        buf.Add(0x1B); buf.Add(0x40);

        // Texto em CP850 (compatível com Bematech)
        var enc = CodePagesEncodingProvider.Instance.GetEncoding(850)
               ?? Encoding.ASCII;
        buf.AddRange(enc.GetBytes(texto));

        // GS V 66 0 — corte parcial
        buf.Add(0x1D); buf.Add(0x56); buf.Add(0x42); buf.Add(0x00);

        return buf.ToArray();
    }

    // ── Formatter 40 chars ───────────────────────────────────────────────

    private static string FormatarCupom(PedidoDetalheDto p)
    {
        var sb = new StringBuilder();
        var hora = p.DataHoraPedido.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

        sb.AppendLine(Sep());
        sb.AppendLine(Centro("  BATATAS FRITAS DELIVERY  "));
        sb.AppendLine(Sep());
        sb.AppendLine(Col($"Pedido #{p.Id}", hora));
        sb.AppendLine(Sep());
        sb.AppendLine($"Cliente : {Trunc(p.NomeCliente, 30)}");
        if (!string.IsNullOrWhiteSpace(p.TelefoneCliente))
            sb.AppendLine($"Tel     : {p.TelefoneCliente}");

        var local = p.TipoAtendimento is "Balcao" or "Totem"
            ? "Retirada no local"
            : p.NomeBairro;
        sb.AppendLine($"Local   : {Trunc(local, 28)}");

        if (!string.IsNullOrWhiteSpace(p.EnderecoEntrega))
            sb.AppendLine($"Endereço: {Trunc(p.EnderecoEntrega, 30)}");

        sb.AppendLine(Sep());
        sb.AppendLine("ITENS:");
        sb.AppendLine(Sep());

        foreach (var item in p.Itens)
        {
            sb.AppendLine(Col($"{item.Quantidade}x {Trunc(item.NomeProduto, 22)}", ""));
            if (!string.IsNullOrWhiteSpace(item.Observacao))
                sb.AppendLine($"   Obs: {item.Observacao}");
        }

        sb.AppendLine(Sep());

        if (p.TaxaEntrega > 0)
            sb.AppendLine(Col("Taxa entrega:", $"R$ {p.TaxaEntrega:F2}"));
        if (p.ValorCashbackUsado > 0)
            sb.AppendLine(Col("Cashback:", $"-R$ {p.ValorCashbackUsado:F2}"));

        sb.AppendLine(Col("TOTAL:", $"R$ {p.ValorTotal:F2}"));
        sb.AppendLine(Sep());

        // Pagamento
        var statusPago = p.StatusPagamento is "Aprovado" or "Presencial" or "PagamentoParcial"
            ? "APROVADO" : "PENDENTE";

        if (!string.IsNullOrWhiteSpace(p.SegundoMetodoPagamento) && p.ValorSegundoPagamento.HasValue)
        {
            var v1 = p.ValorTotal - p.ValorSegundoPagamento.Value;
            sb.AppendLine(Col($"1) {p.MetodoPagamento} ({statusPago}):", $"R$ {v1:F2}"));
            var s2 = p.StatusPagamento == "Aprovado" ? "APROVADO" : "ENTREGA";
            sb.AppendLine(Col($"2) {p.SegundoMetodoPagamento} ({s2}):", $"R$ {p.ValorSegundoPagamento.Value:F2}"));
        }
        else
        {
            sb.AppendLine(Col($"{p.MetodoPagamento}:", statusPago));
        }

        if (p.TrocoPara.HasValue && p.TrocoPara > 0)
            sb.AppendLine(Col("Troco para:", $"R$ {p.TrocoPara:F2}"));

        sb.AppendLine(Sep());
        sb.AppendLine(Centro("Obrigado pela preferencia!"));
        sb.AppendLine();
        sb.AppendLine();

        return sb.ToString();
    }

    private static string Sep()   => new('-', Largura);
    private static string Centro(string s)
    {
        if (s.Length >= Largura) return s;
        var pad = (Largura - s.Length) / 2;
        return s.PadLeft(s.Length + pad).PadRight(Largura);
    }
    private static string Col(string esq, string dir)
    {
        var esp = Largura - esq.Length - dir.Length;
        if (esp < 1) esp = 1;
        return esq + new string(' ', esp) + dir;
    }
    private static string Trunc(string s, int max)
        => s.Length <= max ? s : s[..max];
}
