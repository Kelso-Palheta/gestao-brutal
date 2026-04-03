namespace BatatasFritas.Shared.DTOs;

public class MenuDigitalStatusDto
{
    /// <summary>true = cardápio aberto / false = encerrado</summary>
    public bool Ativo { get; set; }

    /// <summary>Mensagem exibida ao cliente quando o cardápio está encerrado.</summary>
    public string Mensagem { get; set; } = "Atendimento encerrado. Voltamos em breve! 🍟";
}
