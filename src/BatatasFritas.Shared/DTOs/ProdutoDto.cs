using BatatasFritas.Shared.Enums;

namespace BatatasFritas.Shared.DTOs;

public class ProdutoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public CategoriaEnum CategoriaId { get; set; }
    public decimal PrecoBase { get; set; }
    public string ImagemUrl { get; set; } = string.Empty;
    public bool Ativo { get; set; } = true;
    public int Ordem { get; set; } = 0;
    public string ComplementosPermitidos { get; set; } = string.Empty;
    public int EstoqueAtual { get; set; } = 0;
    public int EstoqueMinimo { get; set; } = 0;
}

/// <summary>Item para reordenação batch de produtos.</summary>
public record ReordenarProdutoItem(int Id, int Ordem);
