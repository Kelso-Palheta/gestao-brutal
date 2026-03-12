using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DespesasController : ControllerBase
{
    private readonly IRepository<Despesa> _repo;
    private readonly IUnitOfWork _uow;

    public DespesasController(IRepository<Despesa> repo, IUnitOfWork uow)
    {
        _repo = repo;
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

        var disp = new Despesa(dto.Descricao, dto.Valor, dto.DataRegistro.ToLocalTime(), dto.Categoria);
        
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

    private static DespesaDto ToDto(Despesa d) => new()
    {
        Id = d.Id,
        Descricao = d.Descricao,
        Valor = d.Valor,
        DataRegistro = d.DataRegistro,
        Categoria = d.Categoria
    };
}
