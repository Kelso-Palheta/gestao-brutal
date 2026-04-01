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
    private readonly IRepository<Insumo> _insumoRepository;
    private readonly IRepository<Configuracao> _configRepository;
    private readonly IRepository<Despesa> _despesaRepository;
    private readonly IRepository<TransacaoCashback> _cashbackRepository;
    private readonly IUnitOfWork _uow;

    public FinanceiroController(
        IRepository<Pedido> pedidoRepository,
        IRepository<MovimentacaoEstoque> movRepository,
        IRepository<Insumo> insumoRepository,
        IRepository<Configuracao> configRepository,
        IRepository<Despesa> despesaRepository,
        IRepository<TransacaoCashback> cashbackRepository,
        IUnitOfWork uow)
    {
        _pedidoRepository = pedidoRepository;
        _movRepository = movRepository;
        _insumoRepository = insumoRepository;
        _configRepository = configRepository;
        _despesaRepository = despesaRepository;
        _cashbackRepository = cashbackRepository;
        _uow = uow;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? inicio, [FromQuery] DateTime? fim)
    {
        // Usa horário local (UTC-3, Brasília) para coincidir com o que o operador vê no
        // dashboard (@DateTime.Now). Usa offset fixo para garantir compatibilidade com
        // servidores Linux que podem não ter tzdata instalado.
        var tzBrasilia = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
        var agora     = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzBrasilia);
        var hoje      = agora.Date;
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1);

        var pedidos = await _pedidoRepository.GetAllAsync();
        var movimentacoes = await _movRepository.GetAllAsync();
        var configs = await _configRepository.GetAllAsync();
        var despesasObj = await _despesaRepository.GetAllAsync();

        // Consideramos como faturamento os pedidos efetivamente Pagos.
        var pedidosValidos = pedidos.Where(p => p.StatusPagamento == StatusPagamento.Aprovado || p.StatusPagamento == StatusPagamento.Presencial).ToList();

        var tz = tzBrasilia;

        // --- MÉTRICAS HOJE ---
        var pedidosHoje  = pedidosValidos.Where(p => TimeZoneInfo.ConvertTimeFromUtc(p.DataHoraPedido, tz).Date == hoje).ToList();
        var comprasHoje  = movimentacoes.Where(m => m.Tipo == TipoMovimentacao.Entrada && TimeZoneInfo.ConvertTimeFromUtc(m.DataMovimentacao, tz).Date == hoje).ToList();
        var despesasHoje = despesasObj.Where(d => TimeZoneInfo.ConvertTimeFromUtc(d.DataRegistro, tz).Date == hoje).ToList();

        // --- MÉTRICAS MÊS ---
        var pedidosMes  = pedidosValidos.Where(p => TimeZoneInfo.ConvertTimeFromUtc(p.DataHoraPedido, tz).Date >= inicioMes).ToList();
        var comprasMes  = movimentacoes.Where(m => m.Tipo == TipoMovimentacao.Entrada && TimeZoneInfo.ConvertTimeFromUtc(m.DataMovimentacao, tz).Date >= inicioMes).ToList();
        var despesasMes = despesasObj.Where(d => TimeZoneInfo.ConvertTimeFromUtc(d.DataRegistro, tz).Date >= inicioMes).ToList();

        // --- MÉTRICAS PERÍODO (FILTRO) ---
        var vendasPeriodo = 0m;
        var comprasPeriodo = 0m;
        var despesasPeriodo = 0m;
        var pixPeriodo = 0m;
        var cartaoPeriodo = 0m;
        var dinheiroPeriodo = 0m;
        var totalPedidosPeriodo = 0;

        if (inicio.HasValue && fim.HasValue)
        {
            var dataFimInclusiva = fim.Value.Date.AddDays(1).AddTicks(-1);
            var pPeriodo = pedidosValidos.Where(p => { var d = TimeZoneInfo.ConvertTimeFromUtc(p.DataHoraPedido, tz); return d >= inicio.Value && d <= dataFimInclusiva; }).ToList();
            var cPeriodo = movimentacoes.Where(m => m.Tipo == TipoMovimentacao.Entrada && TimeZoneInfo.ConvertTimeFromUtc(m.DataMovimentacao, tz) >= inicio.Value && TimeZoneInfo.ConvertTimeFromUtc(m.DataMovimentacao, tz) <= dataFimInclusiva).ToList();
            var dPeriodo = despesasObj.Where(d => TimeZoneInfo.ConvertTimeFromUtc(d.DataRegistro, tz) >= inicio.Value && TimeZoneInfo.ConvertTimeFromUtc(d.DataRegistro, tz) <= dataFimInclusiva).ToList();

            vendasPeriodo = pPeriodo.Sum(p => p.ValorTotal);
            comprasPeriodo = cPeriodo.Sum(m => m.ValorTotal);
            despesasPeriodo = dPeriodo.Sum(d => d.Valor);
            totalPedidosPeriodo = pPeriodo.Count;

            pixPeriodo = pPeriodo.Where(p => p.MetodoPagamento == MetodoPagamento.Pix).Sum(p => p.ValorTotal);
            cartaoPeriodo = pPeriodo.Where(p => p.MetodoPagamento == MetodoPagamento.InfiniteTap || p.MetodoPagamento == MetodoPagamento.InfinitePayOnline).Sum(p => p.ValorTotal);
            dinheiroPeriodo = pPeriodo.Where(p => p.MetodoPagamento == MetodoPagamento.Dinheiro).Sum(p => p.ValorTotal);
        }

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
            DespesasHoje = despesasHoje.Sum(d => d.Valor),
            TotalPedidosHoje = pedidosHoje.Count,

            // Métricas Mês
            VendasMes = pedidosMes.Sum(p => p.ValorTotal),
            ComprasMes = comprasMes.Sum(m => m.ValorTotal),
            DespesasMes = despesasMes.Sum(d => d.Valor),

            // Métodos Hoje
            PixHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Pix).Sum(p => p.ValorTotal),
            CartaoHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.InfiniteTap || p.MetodoPagamento == MetodoPagamento.InfinitePayOnline).Sum(p => p.ValorTotal),
            DinheiroHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Dinheiro).Sum(p => p.ValorTotal),

            MetaDiaria = metaDiaria,

            // Métricas Período
            VendasPeriodo = vendasPeriodo,
            ComprasPeriodo = comprasPeriodo,
            DespesasPeriodo = despesasPeriodo,
            TotalPedidosPeriodo = totalPedidosPeriodo,
            PixPeriodo = pixPeriodo,
            CartaoPeriodo = cartaoPeriodo,
            DinheiroPeriodo = dinheiroPeriodo,
            DataInicioFiltro = inicio,
            DataFimFiltro = fim
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

    [HttpPost("limpar-historico")]
    public async Task<IActionResult> LimparHistorico([FromBody] LimparHistoricoDto req)
    {
        // Validação da senha de administrador (Compatível com o hash do ConfiguracoesController)
        var configSenha = await _configRepository.FindAsync(c => c.Chave == "senha_kds");
        bool senhaValida = false;

        if (configSenha == null)
        {
            // Senha padrão se nunca alterado
            senhaValida = req.SenhaAdmin == "palheta2025";
        }
        else
        {
            // Verifica contra o hash BCrypt salvo pelo Admin
            try {
                senhaValida = BCrypt.Net.BCrypt.Verify(req.SenhaAdmin, configSenha.Valor);
            } catch {
                // Caso o valor no banco não seja um hash válido, tenta comparação direta por segurança
                senhaValida = req.SenhaAdmin == configSenha.Valor;
            }
        }

        if (!senhaValida)
        {
            return Unauthorized("Senha administrativa incorreta.");
        }

        if (req.DataFim < req.DataInicio)
        {
            return BadRequest("A data final não pode ser anterior à data inicial.");
        }

        _uow.BeginTransaction();
        try
        {
            // O componente de Time picker pode mandar com a hora 00:00:00, vamos garantir os limites do dia
            var dataFimInclusiva = req.DataFim.Date.AddDays(1).AddTicks(-1);

            switch (req.Tipo.ToLower())
            {
                case "pedidos":
                    var pedidos = await _pedidoRepository.GetAllAsync();
                    var pedidosNoPeriodo = pedidos.Where(p => p.DataHoraPedido >= req.DataInicio && p.DataHoraPedido <= dataFimInclusiva).ToList();
                    foreach (var p in pedidosNoPeriodo) await _pedidoRepository.DeleteAsync(p);
                    break;
                
                case "estoque":
                    // Ao deletar movimentacoes, precisamos reverter o EstoqueAtual dos insumos afetados
                    var movsEstoque = await _movRepository.GetAllAsync();
                    var movsNoPeriodo = movsEstoque.Where(m => m.DataMovimentacao >= req.DataInicio && m.DataMovimentacao <= dataFimInclusiva).ToList();
                    foreach (var m in movsNoPeriodo)
                    {
                        // Reverte o impacto no estoque antes de deletar
                        if (m.Tipo == TipoMovimentacao.Entrada)
                            m.Insumo.AjustarEstoque(-m.Quantidade);
                        else if (m.Tipo == TipoMovimentacao.Saida)
                            m.Insumo.AjustarEstoque(m.Quantidade);
                        else
                            m.Insumo.AjustarEstoque(-m.Quantidade);

                        await _insumoRepository.UpdateAsync(m.Insumo);
                        await _movRepository.DeleteAsync(m);
                    }
                    break;

                case "despesas":
                    var desps = await _despesaRepository.GetAllAsync();
                    var despsNoPeriodo = desps.Where(d => d.DataRegistro >= req.DataInicio && d.DataRegistro <= dataFimInclusiva).ToList();
                    foreach (var d in despsNoPeriodo) await _despesaRepository.DeleteAsync(d);
                    break;

                case "cashback":
                    var cbs = await _cashbackRepository.GetAllAsync();
                    var cbNoPeriodo = cbs.Where(c => c.DataHora >= req.DataInicio && c.DataHora <= dataFimInclusiva).ToList();
                    foreach (var c in cbNoPeriodo) await _cashbackRepository.DeleteAsync(c);
                    break;

                case "financeiro":
                    // Apaga APENAS as Despesas do período (salários, contas, etc.)
                    // NÃO apaga movimentações de estoque para não perder o cadastro de insumos
                    var df = await _despesaRepository.GetAllAsync();
                    foreach (var d in df.Where(d => d.DataRegistro >= req.DataInicio && d.DataRegistro <= dataFimInclusiva).ToList())
                        await _despesaRepository.DeleteAsync(d);
                    break;

                default:
                    await _uow.RollbackAsync();
                    return BadRequest("Tipo de limpeza inválido.");
            }

            await _uow.CommitAsync();
            return Ok(new { Mensagem = $"Histórico de '{req.Tipo}' do período solicitado foi limpo com sucesso!" });
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }
}
