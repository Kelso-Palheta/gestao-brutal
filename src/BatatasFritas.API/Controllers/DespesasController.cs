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

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DespesasController : ControllerBase
{
    private readonly IRepository<Despesa> _repo;
    private readonly NHibernate.ISession _session;
    private readonly IUnitOfWork _uow;

    public DespesasController(IRepository<Despesa> repo, NHibernate.ISession session, IUnitOfWork uow)
    {
        _repo = repo;
        _session = session;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] DateTime? de, [FromQuery] DateTime? ate)
    {
        var todas = await _repo.GetAllAsync();
        
        if (de.HasValue) todas = todas.Where(d => d.DataRegistro.Date >= de.Value.Date);
        if (ate.HasValue) todas = todas.Where(d => d.DataRegistro.Date <= ate.Value.Date);

        return Ok(todas.OrderByDescending(x => x.DataRegistro).Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] DespesaDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Descricao) || dto.Valor <= 0) return BadRequest();

        var disp = new Despesa(dto.Descricao, dto.Valor, dto.DataRegistro.ToLocalTime(), dto.Categoria, dto.Observacao);
        
        _uow.BeginTransaction();
        await _repo.AddAsync(disp);
        await _uow.CommitAsync();

        return Ok(ToDto(disp));
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var disp = await _repo.GetByIdAsync(id);
        if (disp == null) return NotFound();

        _uow.BeginTransaction();
        await _repo.DeleteAsync(disp);
        await _uow.CommitAsync();

        return NoContent();
    }

    // ── DELETE api/despesas/limpar-tudo ────────────────────────────────────
    [HttpDelete("limpar-tudo")]
    public async Task<IActionResult> LimparTudo()
    {
        try
        {
            _uow.BeginTransaction();
            await _session.CreateSQLQuery("DELETE FROM despesas").ExecuteUpdateAsync();
            await _uow.CommitAsync();
            return Ok(new { mensagem = "Todas as despesas foram apagadas." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest($"Erro: {ex.Message}");
        }
    }

    private static DespesaDto ToDto(Despesa d) => new()
    {
        Id = d.Id,
        Descricao = d.Descricao,
        Valor = d.Valor,
        DataRegistro = d.DataRegistro,
        Categoria = d.Categoria,
        Observacao = d.Observacao
    };
}
