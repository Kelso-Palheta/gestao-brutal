using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ComplementosController : ControllerBase
{
    private readonly IRepository<Complemento> _repo;
    private readonly IUnitOfWork _uow;

    public ComplementosController(IRepository<Complemento> repo, IUnitOfWork uow)
    {
        _repo = repo;
        _uow = uow;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var todos = await _repo.GetAllAsync();
        return Ok(todos.OrderBy(x => x.Nome).Select(ToDto).ToList());
    }

    [HttpGet("ativos")]
    public async Task<IActionResult> GetAtivos()
    {
        var todos = await _repo.GetAllAsync();
        return Ok(todos.Where(x => x.Ativo).OrderBy(x => x.Nome).Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ComplementoDto dto)
    {
        var cmp = new Complemento(dto.Nome, dto.Preco, dto.CategoriaAlvo, dto.TipoAcao);
        
        _uow.BeginTransaction();
        await _repo.AddAsync(cmp);
        await _uow.CommitAsync();

        return Ok(ToDto(cmp));
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] ComplementoDto dto)
    {
        var cmp = await _repo.GetByIdAsync(id);
        if (cmp == null) return NotFound();

        cmp.Atualizar(dto.Nome, dto.Preco, dto.CategoriaAlvo, dto.TipoAcao);
        
        _uow.BeginTransaction();
        await _repo.UpdateAsync(cmp);
        await _uow.CommitAsync();

        return Ok(ToDto(cmp));
    }

    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> Toggle(int id, [FromBody] bool ativo)
    {
        var cmp = await _repo.GetByIdAsync(id);
        if (cmp == null) return NotFound();

        if (ativo) cmp.Ativar();
        else cmp.Desativar();

        _uow.BeginTransaction();
        await _repo.UpdateAsync(cmp);
        await _uow.CommitAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var cmp = await _repo.GetByIdAsync(id);
        if (cmp == null) return NotFound();

        _uow.BeginTransaction();
        await _repo.DeleteAsync(cmp);
        await _uow.CommitAsync();

        return NoContent();
    }

    private static ComplementoDto ToDto(Complemento c) => new()
    {
        Id = c.Id,
        Nome = c.Nome,
        Preco = c.Preco,
        CategoriaAlvo = c.CategoriaAlvo,
        TipoAcao = c.TipoAcao,
        Ativo = c.Ativo
    };
}
