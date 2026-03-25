using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

/// <summary>
/// Gerencia autenticação e configurações de acesso do painel KDS/Admin.
/// Armazena a senha como hash BCrypt na tabela `configuracoes`.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ConfiguracoesController : ControllerBase
{
    private const string ChaveSenhaKds = "senha_kds";
    // Senha padrão usada na PRIMEIRA vez que o sistema sobe (sem registro no banco).
    private const string SenhaPadrao = "palheta2025";

    private readonly IRepository<Configuracao> _repo;
    private readonly IUnitOfWork _uow;

    public ConfiguracoesController(IRepository<Configuracao> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    // ─────────────────────────────────────────────────────────────
    // POST api/configuracoes/auth/login — mantido para compatibilidade
    // Para obter JWT use: POST /api/auth/login
    // ─────────────────────────────────────────────────────────────
    [AllowAnonymous]
    [HttpPost("auth/login")]
    public async Task<IActionResult> Login([FromBody] LoginKdsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Senha))
            return BadRequest("Senha não informada.");

        var senhaCorreta = await VerificarSenha(request.Senha);
        if (!senhaCorreta)
            return Unauthorized("Senha incorreta.");

        return Ok(new { autenticado = true });
    }

    // ─────────────────────────────────────────────────────────────
    // POST api/configuracoes/auth/alterar-senha
    // Altera a senha do KDS. Requer a senha atual como confirmação.
    // ─────────────────────────────────────────────────────────────
    [HttpPost("auth/alterar-senha")]
    public async Task<IActionResult> AlterarSenha([FromBody] AlterarSenhaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SenhaAtual) || string.IsNullOrWhiteSpace(request.NovaSenha))
            return BadRequest("Preencha todos os campos.");

        if (request.NovaSenha.Length < 4)
            return BadRequest("A nova senha deve ter ao menos 4 caracteres.");

        // Valida a senha atual antes de alterar
        var senhaOk = await VerificarSenha(request.SenhaAtual);
        if (!senhaOk)
            return Unauthorized("Senha atual incorreta.");

        var novoHash = BCrypt.Net.BCrypt.HashPassword(request.NovaSenha);

        try
        {
            var config = await _repo.FindAsync(c => c.Chave == ChaveSenhaKds);

            _uow.BeginTransaction();
            if (config == null)
            {
                config = new Configuracao(ChaveSenhaKds, novoHash);
                await _repo.AddAsync(config);
            }
            else
            {
                config.Valor = novoHash;
                await _repo.UpdateAsync(config);
            }
            await _uow.CommitAsync();

            return Ok(new { mensagem = "Senha alterada com sucesso!" });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // Helper: busca a senha (hash ou padrão) e verifica
    // ─────────────────────────────────────────────────────────────
    private async Task<bool> VerificarSenha(string senhaInformada)
    {
        var config = await _repo.FindAsync(c => c.Chave == ChaveSenhaKds);

        if (config == null)
        {
            // Ainda sem registro — compara com a senha padrão em texto puro
            return senhaInformada == SenhaPadrao;
        }

        // Registro existe — verifica contra o hash BCrypt
        return BCrypt.Net.BCrypt.Verify(senhaInformada, config.Valor);
    }
}
