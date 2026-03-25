using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NHibernate;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

// GET saldo e GET configuracao são públicos (usados no totem pelo cliente)
// POST configuracao e DELETE são restritos ao operador autenticado
[ApiController]
[Route("api/[controller]")]
public class CashbackController : ControllerBase
{
    public const string ChaveConfigPorcentagem = "cashback_porcentagem";

    private readonly IRepository<CarteiraCashback> _repoCarteira;
    private readonly IRepository<Configuracao> _repoConfig;
    private readonly NHibernate.ISession _session;
    private readonly IUnitOfWork _uow;

    public CashbackController(
        IRepository<CarteiraCashback> repoCarteira,
        IRepository<Configuracao> repoConfig,
        NHibernate.ISession session,
        IUnitOfWork uow)
    {
        _repoCarteira = repoCarteira;
        _repoConfig = repoConfig;
        _session = session;
        _uow = uow;
    }

    [HttpGet("saldo/{telefone}")]
    public async Task<ActionResult<SaldoCashbackDto>> GetSaldo(string telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
            return BadRequest("Telefone é obrigatório.");

        var telLimpo = new string(telefone.Where(char.IsDigit).ToArray());

        var carteira = await _repoCarteira.FindAsync(c => c.Telefone == telLimpo);

        if (carteira == null)
        {
            return Ok(new SaldoCashbackDto { Telefone = telLimpo, NomeCliente = "", SaldoAtual = 0 });
        }

        return Ok(new SaldoCashbackDto
        {
            Telefone = carteira.Telefone,
            NomeCliente = carteira.NomeCliente,
            SaldoAtual = carteira.SaldoAtual
        });
    }

    [HttpGet("configuracao")]
    public async Task<ActionResult<CashbackConfigDto>> GetConfiguracao()
    {
        var config = await _repoConfig.FindAsync(c => c.Chave == ChaveConfigPorcentagem);

        decimal percentual = 0;
        if (config != null && decimal.TryParse(config.Valor, out var p))
        {
            percentual = p;
        }

        return Ok(new CashbackConfigDto { Porcentagem = percentual });
    }

    [Authorize]
    [HttpPost("configuracao")]
    public async Task<IActionResult> SetConfiguracao([FromBody] CashbackConfigDto dto)
    {
        if (dto.Porcentagem < 0 || dto.Porcentagem > 100)
            return BadRequest("Porcentagem deve estar entre 0 e 100.");

        try
        {
            var config = await _repoConfig.FindAsync(c => c.Chave == ChaveConfigPorcentagem);

            _uow.BeginTransaction();
            if (config == null)
            {
                config = new Configuracao(ChaveConfigPorcentagem, dto.Porcentagem.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
                await _repoConfig.AddAsync(config);
            }
            else
            {
                config.Valor = dto.Porcentagem.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                await _repoConfig.UpdateAsync(config);
            }
            await _uow.CommitAsync();

            return Ok(new { mensagem = "Configuração de cashback salva com sucesso!" });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // ── DELETE api/cashback/limpar-tudo ────────────────────────────────────
    [Authorize]
    [HttpDelete("limpar-tudo")]
    public async Task<IActionResult> LimparTudo()
    {
        try
        {
            _uow.BeginTransaction();
            await _session.CreateSQLQuery("DELETE FROM transacoes_cashback").ExecuteUpdateAsync();
            await _session.CreateSQLQuery("DELETE FROM carteiras_cashback").ExecuteUpdateAsync();
            await _uow.CommitAsync();
            return Ok(new { mensagem = "Carteiras e transações de cashback apagadas." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest($"Erro: {ex.Message}");
        }
    }
}
