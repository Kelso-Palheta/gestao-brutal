using System;

namespace BatatasFritas.Shared.DTOs;

public class LimparHistoricoDto
{
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public string SenhaAdmin { get; set; } = string.Empty;
}
