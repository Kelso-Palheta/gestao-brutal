namespace BatatasFritas.Shared.DTOs;

/// <summary>Request para validar a senha no login do KDS.</summary>
public class LoginKdsRequest
{
    public string Senha { get; set; } = string.Empty;
}

/// <summary>Request para alterar a senha do KDS pelo Admin Panel.</summary>
public class AlterarSenhaRequest
{
    public string SenhaAtual  { get; set; } = string.Empty;
    public string NovaSenha   { get; set; } = string.Empty;
}
