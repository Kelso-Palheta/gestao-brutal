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

    // ═══════════════════════════════════════════════════════════
    // DELIVERY (Cardápio Digital)
    // ═══════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────
    // GET api/configuracoes/delivery-status — público (Delivery consulta sem auth)
    // ─────────────────────────────────────────────────────────────
    [AllowAnonymous]
    [HttpGet("delivery-status")]
    public async Task<ActionResult<DeliveryStatusDto>> GetDeliveryStatus()
    {
        var configAtivo    = await _repo.FindAsync(c => c.Chave == "delivery_ativo");
        var configMensagem = await _repo.FindAsync(c => c.Chave == "delivery_mensagem");

        bool ativo = configAtivo == null || configAtivo.Valor == "true"; // padrão: aberto
        string mensagem = configMensagem?.Valor ?? "Atendimento encerrado. Voltamos em breve! 🍟";

        return Ok(new DeliveryStatusDto { Ativo = ativo, Mensagem = mensagem });
    }

    // ─────────────────────────────────────────────────────────────
    // POST api/configuracoes/delivery-status — somente admin
    // ─────────────────────────────────────────────────────────────
    [HttpPost("delivery-status")]
    public async Task<IActionResult> SetDeliveryStatus([FromBody] DeliveryStatusDto dto)
    {
        try
        {
            _uow.BeginTransaction();

            await UpsertConfig("delivery_ativo",    dto.Ativo ? "true" : "false");
            await UpsertConfig("delivery_mensagem", dto.Mensagem ?? "Atendimento encerrado. Voltamos em breve! 🍟");

            await _uow.CommitAsync();
            return Ok(new { mensagem = dto.Ativo ? "Delivery ativado!" : "Delivery encerrado." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // TOTEM (Autoatendimento)
    // ═══════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────
    // GET api/configuracoes/totem-status — público (Totem consulta sem auth)
    // ─────────────────────────────────────────────────────────────
    [AllowAnonymous]
    [HttpGet("totem-status")]
    public async Task<ActionResult<TotemStatusDto>> GetTotemStatus()
    {
        var configAtivo    = await _repo.FindAsync(c => c.Chave == "totem_ativo");
        var configMensagem = await _repo.FindAsync(c => c.Chave == "totem_mensagem");

        bool ativo = configAtivo == null || configAtivo.Valor == "true"; // padrão: aberto
        string mensagem = configMensagem?.Valor ?? "Atendimento encerrado. Voltamos em breve! 🍟";

        return Ok(new TotemStatusDto { Ativo = ativo, Mensagem = mensagem });
    }

    // ─────────────────────────────────────────────────────────────
    // POST api/configuracoes/totem-status — somente admin
    // ─────────────────────────────────────────────────────────────
    [HttpPost("totem-status")]
    public async Task<IActionResult> SetTotemStatus([FromBody] TotemStatusDto dto)
    {
        try
        {
            _uow.BeginTransaction();

            await UpsertConfig("totem_ativo",    dto.Ativo ? "true" : "false");
            await UpsertConfig("totem_mensagem", dto.Mensagem ?? "Atendimento encerrado. Voltamos em breve! 🍟");

            await _uow.CommitAsync();
            return Ok(new { mensagem = dto.Ativo ? "Totem ativado!" : "Totem encerrado." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    // LEGADO: menu-digital (mantido para compatibilidade)
    // ═══════════════════════════════════════════════════════════

    // ─────────────────────────────────────────────────────────────
    // GET api/configuracoes/menu-digital — público (legado)
    // ─────────────────────────────────────────────────────────────
    [AllowAnonymous]
    [HttpGet("menu-digital")]
    public async Task<ActionResult<MenuDigitalStatusDto>> GetMenuDigital()
    {
        // Para compatibilidade: usa delivery_ativo como fallback
        var configAtivo    = await _repo.FindAsync(c => c.Chave == "menu_digital_ativo");
        var configMensagem = await _repo.FindAsync(c => c.Chave == "menu_digital_mensagem");

        // Se não existe configuração legada, usa delivery como fallback
        if (configAtivo == null)
        {
            var deliveryConfig = await _repo.FindAsync(c => c.Chave == "delivery_ativo");
            bool deliveryAtivo = deliveryConfig == null || deliveryConfig.Valor == "true";
            return Ok(new MenuDigitalStatusDto { Ativo = deliveryAtivo, Mensagem = "Use /api/configuracoes/delivery-status" });
        }

        bool ativo = configAtivo.Valor == "true";
        string mensagem = configMensagem?.Valor ?? "Atendimento encerrado. Voltamos em breve! 🍟";

        return Ok(new MenuDigitalStatusDto { Ativo = ativo, Mensagem = mensagem });
    }

    // ─────────────────────────────────────────────────────────────
    // POST api/configuracoes/menu-digital — somente admin (legado)
    // ─────────────────────────────────────────────────────────────
    [HttpPost("menu-digital")]
    public async Task<IActionResult> SetMenuDigital([FromBody] MenuDigitalStatusDto dto)
    {
        try
        {
            _uow.BeginTransaction();

            // Para compatibilidade: atualiza delivery_ativo também
            await UpsertConfig("menu_digital_ativo",    dto.Ativo ? "true" : "false");
            await UpsertConfig("delivery_ativo",        dto.Ativo ? "true" : "false");
            await UpsertConfig("menu_digital_mensagem", dto.Mensagem ?? "Atendimento encerrado. Voltamos em breve! 🍟");
            await UpsertConfig("delivery_mensagem",     dto.Mensagem ?? "Atendimento encerrado. Voltamos em breve! 🍟");

            await _uow.CommitAsync();
            return Ok(new { mensagem = dto.Ativo ? "Cardápio ativado!" : "Cardápio encerrado." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    private async Task UpsertConfig(string chave, string valor)
    {
        var config = await _repo.FindAsync(c => c.Chave == chave);
        if (config == null)
            await _repo.AddAsync(new Configuracao(chave, valor));
        else
        {
            config.Valor = valor;
            await _repo.UpdateAsync(config);
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
