using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FinanceiroController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IRepository<MovimentacaoEstoque> _movRepository;
    private readonly IRepository<Configuracao> _configRepository;
    private readonly IUnitOfWork _uow;

    public FinanceiroController(
        IRepository<Pedido> pedidoRepository,
        IRepository<MovimentacaoEstoque> movRepository,
        IRepository<Configuracao> configRepository,
        IUnitOfWork uow)
    {
        _pedidoRepository = pedidoRepository;
        _movRepository = movRepository;
        _configRepository = configRepository;
        _uow = uow;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        // Usa UTC para consistência com DataHoraPedido (salvo em UTC pelo domínio)
        var hoje      = DateTime.UtcNow.Date;
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var pedidos = await _pedidoRepository.GetAllAsync();
        var movimentacoes = await _movRepository.GetAllAsync();
        var configs = await _configRepository.GetAllAsync();

        // Consideramos como faturamento os pedidos Entregues (que de fato ocorreram e geraram receita local)
        // Pedidos cancelados não entram na conta financeira.
        var pedidosValidos = pedidos.Where(p => p.Status == StatusPedido.Entregue).ToList();

        // Filtro por período
        var pedidosHoje = pedidosValidos.Where(p => p.DataHoraPedido.Date == hoje).ToList();
        var pedidosMes = pedidosValidos.Where(p => p.DataHoraPedido.Date >= inicioMes).ToList();

        // Entradas de Estoque (Compras)
        var compras = movimentacoes.Where(m => m.Tipo == TipoMovimentacao.Entrada).ToList();
        var comprasHoje = compras.Where(m => m.DataMovimentacao.Date == hoje).ToList();
        var comprasMes = compras.Where(m => m.DataMovimentacao.Date >= inicioMes).ToList();

        // Leitura de Meta Diária
        decimal metaDiaria = 0m;
        var configMeta = configs.FirstOrDefault(c => c.Chave == "meta_diaria_vendas");
        if (configMeta != null && decimal.TryParse(configMeta.Valor, out var metaObj))
        {
            metaDiaria = metaObj;
        }

        var dto = new FinanceiroDashboardDto
        {
            // Métricas Hoje
            VendasHoje = pedidosHoje.Sum(p => p.ValorTotal),
            ComprasHoje = comprasHoje.Sum(m => m.ValorTotal),
            TotalPedidosHoje = pedidosHoje.Count,

            // Métricas Mês
            VendasMes = pedidosMes.Sum(p => p.ValorTotal),
            ComprasMes = comprasMes.Sum(m => m.ValorTotal),

            // Métodos Hoje
            PixHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Pix).Sum(p => p.ValorTotal),
            CartaoHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.InfiniteTap || p.MetodoPagamento == MetodoPagamento.InfinitePayOnline).Sum(p => p.ValorTotal),
            DinheiroHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Dinheiro).Sum(p => p.ValorTotal),

            MetaDiaria = metaDiaria
        };

        return Ok(dto);
    }

    [HttpPost("meta")]
    public async Task<IActionResult> SalvarMetaDiaria([FromBody] decimal novaMeta)
    {
        try
        {
            var configMeta = await _configRepository.FindAsync(c => c.Chave == "meta_diaria_vendas");

            _uow.BeginTransaction();
            if (configMeta == null)
            {
                configMeta = new Configuracao("meta_diaria_vendas", novaMeta.ToString("F2"));
                await _configRepository.AddAsync(configMeta);
            }
            else
            {
                configMeta.Valor = novaMeta.ToString("F2");
                await _configRepository.UpdateAsync(configMeta);
            }

            await _uow.CommitAsync();
            return Ok(new { Mensagem = "Meta salva com sucesso!" });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }
}
