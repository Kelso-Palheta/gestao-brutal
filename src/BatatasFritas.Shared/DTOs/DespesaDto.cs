using System;

namespace BatatasFritas.Shared.DTOs;

public class DespesaDto
{
    public int Id { get; set; }
    public string Descricao { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime DataRegistro { get; set; }
    public string Categoria { get; set; } = "Outros";
    public string? Observacao { get; set; }
}

public class ExtrairNfRequest
{
    /// <summary>Imagem da NF em base64 (sem o prefixo data:image/...).</summary>
    public string ImagemBase64 { get; set; } = string.Empty;
    public string MimeType { get; set; } = "image/jpeg";
}

public class ExtrairNfResponse
{
    public bool Sucesso { get; set; }
    public string? Erro { get; set; }
    public DespesaDto Despesa { get; set; } = new();
    public string? NumeroNF { get; set; }
    public string? CnpjEmitente { get; set; }
    public string? NomeEmitente { get; set; }
}
