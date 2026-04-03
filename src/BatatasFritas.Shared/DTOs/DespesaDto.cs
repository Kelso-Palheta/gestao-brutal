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
