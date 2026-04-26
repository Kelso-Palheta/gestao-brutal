using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string ChaveSenhaKds = "senha_kds_hash";
    private const string LITERAL_REJEITADO = "palheta2025";

    private readonly IRepository<Configuracao> _repo;
    private readonly IConfiguration _config;

    private static string? SenhaPadrao =>
        Environment.GetEnvironmentVariable("KDS_DEFAULT_PASSWORD");

    public AuthController(IRepository<Configuracao> repo, IConfiguration config)
    {
        _repo   = repo;
        _config = config;
    }

    /// <summary>
    /// Login do operador KDS. Retorna um JWT válido por 8h.
    /// POST /api/auth/login  { "senha": "..." }
    /// </summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Senha))
            return BadRequest("Senha é obrigatória.");

        var senhaOk = await VerificarSenha(request.Senha);
        if (!senhaOk)
            return Unauthorized(new { mensagem = "Senha incorreta." });

        var token = GerarToken();
        return Ok(new { token });
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<bool> VerificarSenha(string senha)
    {
        // Bloqueia o literal antigo em qualquer cenário (defesa contra reuso)
        if (senha == LITERAL_REJEITADO) return false;

        var config = await _repo.FindAsync(c => c.Chave == ChaveSenhaKds);
        if (config == null)
        {
            // Sem senha persistida: aceita apenas se KDS_DEFAULT_PASSWORD estiver setada
            // e não for o literal antigo. Comparação constant-time.
            var padrao = SenhaPadrao;
            if (string.IsNullOrEmpty(padrao) || padrao == LITERAL_REJEITADO) return false;
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(senha),
                Encoding.UTF8.GetBytes(padrao));
        }

        return BCrypt.Net.BCrypt.Verify(senha, config.Valor);
    }

    private string GerarToken()
    {
        var secretKey  = _config["Jwt:SecretKey"]  ?? throw new InvalidOperationException("Jwt:SecretKey não configurado.");
        var issuer     = _config["Jwt:Issuer"]     ?? "BatatasFritasAPI";
        var audience   = _config["Jwt:Audience"]   ?? "BatatasFritasKDS";
        var expMinutes = int.TryParse(_config["Jwt:ExpirationMinutes"], out var m) ? m : 480;

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "operador"),
            new Claim(ClaimTypes.Role, "KDS")
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(expMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Senha { get; set; } = string.Empty;
}
