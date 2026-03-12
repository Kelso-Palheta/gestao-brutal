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
public class BairrosController : ControllerBase
{
    private readonly IRepository<Bairro> _bairroRepository;
    private readonly IUnitOfWork _uow;

    public BairrosController(IRepository<Bairro> bairroRepository, IUnitOfWork uow)
    {
        _bairroRepository = bairroRepository;
        _uow = uow;
    }

    // GET api/bairros
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var bairros = await _bairroRepository.GetAllAsync();
        var dtos = bairros.Select(b => new BairroDto
        {
            Id = b.Id,
            Nome = b.Nome,
            TaxaEntrega = b.TaxaEntrega
        }).OrderBy(b => b.Nome).ToList();
        
        return Ok(dtos);
    }

    // POST api/bairros — cria novo bairro
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] BairroDto dto)
    {
        var bairro = new Bairro(dto.Nome, dto.TaxaEntrega);
        
        _uow.BeginTransaction();
        await _bairroRepository.AddAsync(bairro);
        await _uow.CommitAsync();

        return Ok(new { bairro.Id, bairro.Nome });
    }

    // PUT api/bairros/{id} — atualiza nome e taxa de entrega
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] BairroDto dto)
    {
        var bairro = await _bairroRepository.GetByIdAsync(id);
        if (bairro == null) return NotFound("Bairro não encontrado.");

        bairro.Atualizar(dto.Nome, dto.TaxaEntrega);

        _uow.BeginTransaction();
        await _bairroRepository.UpdateAsync(bairro);
        await _uow.CommitAsync();

        return Ok(new { bairro.Id, bairro.Nome });
    }

    // DELETE api/bairros/{id} — remove bairro
    // Retorna 409 se houver pedidos vinculados a este bairro.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var bairro = await _bairroRepository.GetByIdAsync(id);
        if (bairro == null) return NotFound("Bairro não encontrado.");

        try
        {
            _uow.BeginTransaction();
            await _bairroRepository.DeleteAsync(bairro);
            await _uow.CommitAsync();
            return NoContent();
        }
        catch (Exception ex) when (
            ex.Message.Contains("FOREIGN KEY") ||
            ex.Message.Contains("foreign key") ||
            ex.Message.Contains("constraint") ||
            ex.InnerException?.Message.Contains("constraint") == true)
        {
            return Conflict($"O bairro '{bairro.Nome}' está vinculado a pedidos existentes e não pode ser excluído. Edite a taxa de entrega ou crie um novo bairro.");
        }
    }
}

