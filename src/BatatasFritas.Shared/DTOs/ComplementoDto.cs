using System;

namespace BatatasFritas.Shared.DTOs;

public class ComplementoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public string CategoriaAlvo { get; set; } = "Todas"; // "Todas", "Batatas", "Bebidas", "Porcoes", "Sobremesas"
    public string TipoAcao { get; set; } = "AdicionalPago"; // "AdicionalPago", "MolhoGratuito", "Remocao"
    public bool Ativo { get; set; } = true;
}
