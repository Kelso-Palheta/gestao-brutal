using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InsumosController : ControllerBase
{
    private readonly IRepository<Insumo> _insumoRepo;
    private readonly IRepository<MovimentacaoEstoque> _movRepo;
    private readonly IUnitOfWork _uow;

    public InsumosController(
        IRepository<Insumo> insumoRepo,
        IRepository<MovimentacaoEstoque> movRepo,
        IUnitOfWork uow)
    {
        _insumoRepo = insumoRepo;
        _movRepo    = movRepo;
        _uow        = uow;
    }

    // ── GET api/insumos/dashboard ─────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] string? de = null,
        [FromQuery] string? ate = null)
    {
        var insumos   = (await _insumoRepo.GetAllAsync()).Where(i => i.Ativo).ToList();
        var movs      = (await _movRepo.GetAllAsync()).ToList();

        if (DateTime.TryParse(de,  out var ini)) movs = movs.Where(m => m.DataMovimentacao >= ini).ToList();
        if (DateTime.TryParse(ate, out var fim)) movs = movs.Where(m => m.DataMovimentacao <= fim.AddDays(1)).ToList();

        var totalGastos = movs
            .Where(m => m.Tipo == TipoMovimentacao.Entrada)
            .Sum(m => m.ValorTotal);

        var valorEstoque = insumos.Sum(i => i.EstoqueAtual * i.CustoPorUnidade);
        var alertas      = insumos.Where(i => i.AbaixoDoMinimo).ToList();

        var dto = new EstoqueDashboardDto
        {
            TotalInsumos          = insumos.Count,
            InsumosAbaixoMinimo   = alertas.Count,
            TotalGastosCompras    = totalGastos,
            ValorEstoqueAtual     = valorEstoque,
            AlertasEstoque        = alertas.Select(ToDto).ToList(),
            UltimasMovimentacoes  = movs
                .OrderByDescending(m => m.DataMovimentacao)
                .Take(20)
                .Select(ToMovDto)
                .ToList()
        };

        return Ok(dto);
    }

    // ── GET api/insumos ───────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var insumos = (await _insumoRepo.GetAllAsync())
            .Where(i => i.Ativo)
            .OrderBy(i => i.Nome)
            .Select(ToDto)
            .ToList();
        return Ok(insumos);
    }

    // ── GET api/insumos/todos (incluindo inativos) ───────────────────────
    [HttpGet("todos")]
    public async Task<IActionResult> GetTodos()
    {
        var insumos = (await _insumoRepo.GetAllAsync())
            .OrderBy(i => i.Nome)
            .Select(ToDto)
            .ToList();
        return Ok(insumos);
    }

    // ── POST api/insumos ──────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] InsumoDto dto)
    {
        var insumo = new Insumo(dto.Nome, dto.Unidade, dto.EstoqueMinimo, dto.CustoPorUnidade);
        _uow.BeginTransaction();
        await _insumoRepo.AddAsync(insumo);
        await _uow.CommitAsync();
        return Ok(new { insumo.Id, insumo.Nome });
    }

    // ── PUT api/insumos/{id} ──────────────────────────────────────────────
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] InsumoDto dto)
    {
        var insumo = await _insumoRepo.GetByIdAsync(id);
        if (insumo == null) return NotFound();

        insumo.Atualizar(dto.Nome, dto.Unidade, dto.EstoqueMinimo, dto.CustoPorUnidade);
        _uow.BeginTransaction();
        await _insumoRepo.UpdateAsync(insumo);
        await _uow.CommitAsync();
        return Ok();
    }

    // ── DELETE api/insumos/{id} ───────────────────────────────────────────
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var insumo = await _insumoRepo.GetByIdAsync(id);
        if (insumo == null) return NotFound();

        insumo.Ativo = false;
        _uow.BeginTransaction();
        await _insumoRepo.UpdateAsync(insumo);
        await _uow.CommitAsync();
        return NoContent();
    }

    [HttpPost("{id}/restaurar")]
    public async Task<IActionResult> Restaurar(int id)
    {
        var insumo = await _insumoRepo.GetByIdAsync(id);
        if (insumo == null) return NotFound();

        insumo.Ativo = true;
        _uow.BeginTransaction();
        await _insumoRepo.UpdateAsync(insumo);
        await _uow.CommitAsync();
        return Ok(new { msg = "Insumo restaurado com sucesso!" });
    }

    // ── PATCH api/insumos/{id}/ajustar-saldo ─────────────────────────────
    [HttpPatch("{id}/ajustar-saldo")]
    public async Task<IActionResult> AjustarSaldo(int id, [FromBody] AjusteSaldoRequest req)
    {
        var insumo = await _insumoRepo.GetByIdAsync(id);
        if (insumo == null) return NotFound();

        decimal diferenca = req.NovoSaldo - insumo.EstoqueAtual;
        if (diferenca == 0) return Ok(new { msg = "O saldo informado é igual ao atual." });

        var mov = new MovimentacaoEstoque(insumo, TipoMovimentacao.Ajuste, diferenca, insumo.CustoPorUnidade, req.Motivo ?? "Ajuste direto de saldo");

        _uow.BeginTransaction();
        await _movRepo.AddAsync(mov);
        await _insumoRepo.UpdateAsync(insumo);
        await _uow.CommitAsync();

        return Ok(new { msg = "Saldo ajustado!", novoSaldo = insumo.EstoqueAtual });
    }

    // ── POST api/insumos/movimentar ───────────────────────────────────────
    [HttpPost("movimentar")]
    public async Task<IActionResult> Movimentar([FromBody] NovaMovimentacaoRequest req)
    {
        var insumo = await _insumoRepo.GetByIdAsync(req.InsumoId);
        if (insumo == null) return NotFound("Insumo não encontrado.");

        if (!Enum.TryParse<TipoMovimentacao>(req.Tipo, out var tipo))
            return BadRequest("Tipo inválido. Use: Entrada, Saida ou Ajuste.");

        if (tipo == TipoMovimentacao.Saida && insumo.EstoqueAtual < req.Quantidade)
            return BadRequest($"Estoque insuficiente. Disponível: {insumo.EstoqueAtual} {insumo.Unidade}.");

        var mov = new MovimentacaoEstoque(insumo, tipo, req.Quantidade, req.ValorUnitario,
            req.Motivo, req.Fornecedor, req.NumeroNF);

        _uow.BeginTransaction();
        await _movRepo.AddAsync(mov);
        await _insumoRepo.UpdateAsync(insumo);
        await _uow.CommitAsync();

        return Ok(new { mov.Id, NovoEstoque = insumo.EstoqueAtual, insumo.AbaixoDoMinimo });
    }

    // ── GET api/insumos/movimentacoes?de=&ate= ────────────────────────────
    [HttpGet("movimentacoes")]
    public async Task<IActionResult> Movimentacoes(
        [FromQuery] string? de  = null,
        [FromQuery] string? ate = null)
    {
        var todas = (await _movRepo.GetAllAsync()).ToList();

        if (DateTime.TryParse(de,  out var ini2)) todas = todas.Where(m => m.DataMovimentacao >= ini2).ToList();
        if (DateTime.TryParse(ate, out var fim2)) todas = todas.Where(m => m.DataMovimentacao <= fim2.AddDays(1)).ToList();

        return Ok(todas.OrderByDescending(m => m.DataMovimentacao).Select(ToMovDto).ToList());
    }

    // ── DELETE api/insumos/limpar-movimentacoes ───────────────────────────
    [HttpDelete("limpar-movimentacoes")]
    public async Task<IActionResult> LimparMovimentacoes()
    {
        try
        {
            _uow.BeginTransaction();
            await _uow.ExecuteHqlAsync("DELETE FROM MovimentacaoEstoque");
            await _uow.ExecuteHqlAsync("UPDATE Insumo SET EstoqueAtual = 0");
            await _uow.CommitAsync();
            return Ok(new { mensagem = "Movimentações apagadas e estoques zerados." });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest($"Erro: {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private static InsumoDto ToDto(Insumo i) => new()
    {
        Id             = i.Id,
        Nome           = i.Nome,
        Unidade        = i.Unidade,
        EstoqueAtual   = i.EstoqueAtual,
        EstoqueMinimo  = i.EstoqueMinimo,
        CustoPorUnidade = i.CustoPorUnidade,
        Ativo          = i.Ativo,
        AbaixoDoMinimo = i.AbaixoDoMinimo
    };

    private static MovimentacaoDto ToMovDto(MovimentacaoEstoque m) => new()
    {
        Id                = m.Id,
        InsumoId          = m.Insumo.Id,
        InsumoNome        = m.Insumo.Nome,
        Tipo              = m.Tipo.ToString(),
        Quantidade        = m.Quantidade,
        ValorUnitario     = m.ValorUnitario,
        ValorTotal        = m.ValorTotal,
        DataMovimentacao  = m.DataMovimentacao,
        Motivo            = m.Motivo,
        Fornecedor        = m.Fornecedor,
        NumeroNF          = m.NumeroNF
    };
}
