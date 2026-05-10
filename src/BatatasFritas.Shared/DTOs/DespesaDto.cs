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

    // ── Compra de Insumo (opcionais) ─────────────────────────────────
    /// <summary>Insumo comprado. Quando preenchido junto com QuantidadeInsumo,
    /// cria MovimentacaoEstoque (Entrada) automaticamente ao salvar.</summary>
    public int? InsumoId { get; set; }
    public decimal? QuantidadeInsumo { get; set; }
    /// <summary>Número da NF propagado para MovimentacaoEstoque.NumeroNF.</summary>
    public string? NumeroNFMovimentacao { get; set; }
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
    /// <summary>Itens extraídos da NF com insumo sugerido por correspondência de nome.</summary>
    public List<ExtrairNfItemResponse> Itens { get; set; } = new();
}

/// <summary>Um produto extraído da NF com insumo sugerido.</summary>
public class ExtrairNfItemResponse
{
    public string NomeProduto { get; set; } = string.Empty;
    public string Unidade { get; set; } = "un";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    /// <summary>Id do insumo correspondente encontrado pelo nome (null = nenhum).</summary>
    public int? InsumoId { get; set; }
    public string? InsumoNome { get; set; }
    /// <summary>true = insumo não existia e deve ser criado ao confirmar.</summary>
    public bool InsumoNovo { get; set; }
}

/// <summary>Payload para POST api/despesas/confirmar-nf.</summary>
public class ConfirmarNfRequest
{
    public DespesaDto Despesa { get; set; } = new();
    public string NumeroNF { get; set; } = string.Empty;
    public List<ConfirmarNfItemDto> Itens { get; set; } = new();
}

public class ConfirmarNfItemDto
{
    /// <summary>Id do insumo existente. null quando CriarInsumo=true.</summary>
    public int? InsumoId { get; set; }
    public string NomeProduto { get; set; } = string.Empty;
    public string Unidade { get; set; } = "un";
    public decimal Quantidade { get; set; }
    public decimal ValorUnitario { get; set; }
    /// <summary>true = criar novo Insumo com NomeProduto/Unidade/ValorUnitario.</summary>
    public bool CriarInsumo { get; set; }
}
