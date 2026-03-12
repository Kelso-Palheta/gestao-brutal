using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CashbackController : ControllerBase
{
    public const string ChaveConfigPorcentagem = "cashback_porcentagem";

    private readonly IRepository<CarteiraCashback> _repoCarteira;
    private readonly IRepository<Configuracao> _repoConfig;
    private readonly IUnitOfWork _uow;

    public CashbackController(
        IRepository<CarteiraCashback> repoCarteira,
        IRepository<Configuracao> repoConfig,
        IUnitOfWork uow)
    {
        _repoCarteira = repoCarteira;
        _repoConfig = repoConfig;
        _uow = uow;
    }

    // ─────────────────────────────────────────────────────────────
    // GET api/cashback/saldo/{telefone}
    // Retorna o saldo da carteira do cliente pelo telefone.
    // Se a carteira não existir, ela não é criada, apenas retorna 0.
    // ─────────────────────────────────────────────────────────────
    [HttpGet("saldo/{telefone}")]
    public async Task<ActionResult<SaldoCashbackDto>> GetSaldo(string telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
            return BadRequest("Telefone é obrigatório.");

        // Limpa formatação do telefone para garantir busca exata
        var telLimpo = new string(telefone.Where(char.IsDigit).ToArray());

        var todas = await _repoCarteira.GetAllAsync();
        var carteira = todas.FirstOrDefault(c => c.Telefone == telLimpo);

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

    // ─────────────────────────────────────────────────────────────
    // GET api/cashback/configuracao
    // Retorna a porcentagem de cashback configurada no banco.
    // ─────────────────────────────────────────────────────────────
    [HttpGet("configuracao")]
    public async Task<ActionResult<CashbackConfigDto>> GetConfiguracao()
    {
        var configs = await _repoConfig.GetAllAsync();
        var config = configs.FirstOrDefault(c => c.Chave == ChaveConfigPorcentagem);

        decimal percentual = 0;
        if (config != null && decimal.TryParse(config.Valor, out var p))
        {
            percentual = p;
        }

        return Ok(new CashbackConfigDto { Porcentagem = percentual });
    }

    // ─────────────────────────────────────────────────────────────
    // POST api/cashback/configuracao
    // Salva a nova porcentagem de cashback no banco.
    // ─────────────────────────────────────────────────────────────
    [HttpPost("configuracao")]
    public async Task<IActionResult> SetConfiguracao([FromBody] CashbackConfigDto dto)
    {
        if (dto.Porcentagem < 0 || dto.Porcentagem > 100)
            return BadRequest("Porcentagem deve estar entre 0 e 100.");

        var configs = await _repoConfig.GetAllAsync();
        var config = configs.FirstOrDefault(c => c.Chave == ChaveConfigPorcentagem);

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
}
