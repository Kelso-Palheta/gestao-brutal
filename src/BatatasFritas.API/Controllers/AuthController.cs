using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private const string ChaveSenhaKds = "senha_kds_hash";
    private const string SenhaPadrao   = "palheta2025"; // Fallback apenas se nunca foi definida

    private readonly IRepository<Configuracao> _repo;
    private readonly IConfiguration _config;

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
        var config = await _repo.FindAsync(c => c.Chave == ChaveSenhaKds);
        if (config == null)
            return senha == SenhaPadrao;

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
