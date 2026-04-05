namespace BatatasFritas.Shared.DTOs;

public class MenuDigitalStatusDto
{
    /// <summary>true = cardápio aberto / false = encerrado</summary>
    public bool Ativo { get; set; }

    /// <summary>Mensagem exibida ao cliente quando o cardápio está encerrado.</summary>
    public string Mensagem { get; set; } = "Atendimento encerrado. Voltamos em breve! 🍟";
}

/// <summary>
/// Status do cardápio Delivery (separado do Totem)
/// </summary>
public class DeliveryStatusDto
{
    /// <summary>true = delivery aberto / false = encerrado</summary>
    public bool Ativo { get; set; }

    /// <summary>Mensagem exibida ao cliente quando o delivery está encerrado.</summary>
    public string Mensagem { get; set; } = "Atendimento encerrado. Voltamos em breve! 🍟";
}

/// <summary>
/// Status do Totem (separado do Delivery)
/// </summary>
public class TotemStatusDto
{
    /// <summary>true = totem ativo / false = encerrado</summary>
    public bool Ativo { get; set; }

    /// <summary>Mensagem exibida ao cliente quando o totem está encerrado.</summary>
    public string Mensagem { get; set; } = "Atendimento encerrado. Voltamos em breve! 🍟";
}