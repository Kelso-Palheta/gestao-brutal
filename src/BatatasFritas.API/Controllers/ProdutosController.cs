using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProdutosController : ControllerBase
{
    private readonly IRepository<Produto> _produtoRepository;
    private readonly IRepository<ItemReceita> _receitaRepository;
    private readonly IRepository<Insumo> _insumoRepository;
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<PedidosHub> _hub;

    public ProdutosController(
        IRepository<Produto> produtoRepository, 
        IRepository<ItemReceita> receitaRepository,
        IRepository<Insumo> insumoRepository,
        IUnitOfWork uow,
        IHubContext<PedidosHub> hub)
    {
        _produtoRepository = produtoRepository;
        _receitaRepository = receitaRepository;
        _insumoRepository = insumoRepository;
        _uow = uow;
        _hub = hub;
    }

    // GET api/produtos — retorna todos, ordenados pelo campo Ordem
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var produtos = await _produtoRepository.GetAllAsync();
        var dtos = produtos.Select(p => new ProdutoDto
        {
            Id = p.Id,
            Nome = p.Nome,
            Descricao = p.Descricao,
            CategoriaId = p.CategoriaId,
            PrecoBase = p.PrecoBase,
            ImagemUrl = p.ImagemUrl,
            Ativo = p.Ativo,
            Ordem = p.Ordem,
            ComplementosPermitidos = p.ComplementosPermitidos,
            EstoqueAtual = p.EstoqueAtual,
            EstoqueMinimo = p.EstoqueMinimo
        }).OrderBy(p => p.Ordem).ThenBy(p => p.CategoriaId).ThenBy(p => p.Nome).ToList();

        return Ok(dtos);
    }

    // POST api/produtos — cria novo produto
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ProdutoDto dto)
    {
        try
        {
            var produto = new Produto(dto.Nome, dto.Descricao, dto.CategoriaId, dto.PrecoBase, dto.ImagemUrl, dto.ComplementosPermitidos, dto.EstoqueAtual, dto.EstoqueMinimo);
            _uow.BeginTransaction();
            await _produtoRepository.AddAsync(produto);
            await _uow.CommitAsync();
            return Ok(new { produto.Id, dto.Nome });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // PUT api/produtos/{id} — edita produto existente
    [HttpPut("{id}")]
    public async Task<IActionResult> Put(int id, [FromBody] ProdutoDto dto)
    {
        var produto = await _produtoRepository.GetByIdAsync(id);
        if (produto == null) return NotFound("Produto não encontrado.");

        try
        {
            produto.Atualizar(dto.Nome, dto.Descricao, dto.CategoriaId, dto.PrecoBase, dto.ImagemUrl, dto.ComplementosPermitidos);
            
            // Atualiza estoque
            bool estavaSemEstoque = produto.EstoqueAtual <= 0;
            produto.EstoqueAtual = dto.EstoqueAtual;
            produto.EstoqueMinimo = dto.EstoqueMinimo;
            
            // Se o produto estava desativado por falta de estoque e agora tem estoque, reativa automaticamente
            if (estavaSemEstoque && produto.EstoqueAtual > 0 && !produto.Ativo)
            {
                produto.Ativar();
                await _hub.Clients.All.SendAsync("ProdutoReativado", new { produto.Id, produto.Nome, produto.EstoqueAtual });
            }
            
            _uow.BeginTransaction();
            await _produtoRepository.UpdateAsync(produto);
            await _uow.CommitAsync();
            return Ok(new { produto.Id, produto.Nome, produto.EstoqueAtual, produto.Ativo });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // PATCH api/produtos/{id}/ativar — ativa produto no cardápio
    [HttpPatch("{id}/ativar")]
    public async Task<IActionResult> Ativar(int id)
    {
        var produto = await _produtoRepository.GetByIdAsync(id);
        if (produto == null) return NotFound();

        try
        {
            // Não permite reativar produto sem estoque — o admin deve repor via PUT antes de ativar
            if (produto.EstoqueAtual <= 0)
                return BadRequest($"Não é possível ativar '{produto.Nome}' com estoque zerado. Acesse 'Editar Produto' e atualize o estoque antes de reativar.");

            produto.Ativar();

            _uow.BeginTransaction();
            await _produtoRepository.UpdateAsync(produto);
            await _uow.CommitAsync();

            // Notifica o Totem/Cardápio Digital via SignalR
            await _hub.Clients.All.SendAsync("ProdutoReativado", new { produto.Id, produto.Nome, produto.EstoqueAtual });

            return Ok(new { produto.Id, produto.Ativo, produto.EstoqueAtual });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // PATCH api/produtos/{id}/desativar — remove produto do cardápio (sem deletar)
    [HttpPatch("{id}/desativar")]
    public async Task<IActionResult> Desativar(int id)
    {
        var produto = await _produtoRepository.GetByIdAsync(id);
        if (produto == null) return NotFound();

        try
        {
            produto.Desativar();
            _uow.BeginTransaction();
            await _produtoRepository.UpdateAsync(produto);
            await _uow.CommitAsync();
            return Ok(new { produto.Id, produto.Ativo });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // PATCH api/produtos/reordenar — salva nova ordem da lista inteira em uma transação
    [HttpPatch("reordenar")]
    public async Task<IActionResult> Reordenar([FromBody] List<ReordenarProdutoItem> itens)
    {
        if (itens == null || !itens.Any())
            return BadRequest("Lista de reordenação vazia.");

        try
        {
            _uow.BeginTransaction();
            foreach (var item in itens)
            {
                var produto = await _produtoRepository.GetByIdAsync(item.Id);
                if (produto != null)
                {
                    produto.Ordem = item.Ordem;
                    await _produtoRepository.UpdateAsync(produto);
                }
            }
            await _uow.CommitAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    // DELETE api/produtos/{id} — tenta remover permanentemente.
    // Se houver pedidos vinculados (FK constraint), faz soft-delete (desativa) automaticamente.
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var produto = await _produtoRepository.GetByIdAsync(id);
        if (produto == null) return NotFound("Produto não encontrado.");

        try
        {
            _uow.BeginTransaction();
            await _produtoRepository.DeleteAsync(produto);
            await _uow.CommitAsync();
            return NoContent();
        }
        catch (Exception ex) when (
            ex.Message.Contains("FOREIGN KEY") ||
            ex.Message.Contains("foreign key") ||
            ex.Message.Contains("constraint") ||
            ex.InnerException?.Message.Contains("constraint") == true)
        {
            // Produto tem pedidos vinculados — faz soft-delete em vez de excluir fisicamente
            try
            {
                _uow.BeginTransaction();
                produto.Desativar();
                await _produtoRepository.UpdateAsync(produto);
                await _uow.CommitAsync();
                return Ok(new
                {
                    softDelete = true,
                    mensagem = $"'{produto.Nome}' possui pedidos vinculados e foi desativado do cardápio (não excluído). Para excluir permanentemente, remova os pedidos primeiro."
                });
            }
            catch
            {
                return StatusCode(500, "Erro ao desativar produto.");
            }
        }
    }

    // ── GET api/produtos/{id}/receita ────────────────────────────────────
    [HttpGet("{id}/receita")]
    public async Task<IActionResult> GetReceita(int id)
    {
        var receitas = await _receitaRepository.GetAllAsync();
        var lista = receitas.Where(r => r.Produto.Id == id).Select(r => new ItemReceitaDto
        {
            Id = r.Id,
            ProdutoId = r.Produto.Id,
            InsumoId = r.Insumo.Id,
            InsumoNome = r.Insumo.Nome,
            InsumoUnidade = r.Insumo.Unidade,
            QuantidadePorUnidade = r.QuantidadePorUnidade
        }).ToList();

        return Ok(lista);
    }

    // ── POST api/produtos/{id}/receita ───────────────────────────────────
    [HttpPost("{id}/receita")]
    public async Task<IActionResult> AddItemReceita(int id, [FromBody] NovoItemReceitaDto dto)
    {
        var produto = await _produtoRepository.GetByIdAsync(id);
        if (produto == null) return NotFound("Produto não encontrado");

        var insumo = await _insumoRepository.GetByIdAsync(dto.InsumoId);
        if (insumo == null) return NotFound("Insumo não encontrado");

        var itemReceita = new ItemReceita(produto, insumo, dto.QuantidadePorUnidade);
        
        _uow.BeginTransaction();
        await _receitaRepository.AddAsync(itemReceita);
        await _uow.CommitAsync();

        return Ok();
    }

    // ── DELETE api/produtos/{id}/receita/{receitaId} ─────────────────────
    [HttpDelete("{id}/receita/{receitaId}")]
    public async Task<IActionResult> RemoveItemReceita(int id, int receitaId)
    {
        var receita = await _receitaRepository.GetByIdAsync(receitaId);
        if (receita == null || receita.Produto.Id != id) 
            return NotFound();

        _uow.BeginTransaction();
        await _receitaRepository.DeleteAsync(receita);
        await _uow.CommitAsync();

        return NoContent();
    }
}

