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
    private readonly IRepository<Despesa> _despesaRepository;
    private readonly IRepository<TransacaoCashback> _cashbackRepository;
    private readonly IUnitOfWork _uow;

    public FinanceiroController(
        IRepository<Pedido> pedidoRepository,
        IRepository<MovimentacaoEstoque> movRepository,
        IRepository<Configuracao> configRepository,
        IRepository<Despesa> despesaRepository,
        IRepository<TransacaoCashback> cashbackRepository,
        IUnitOfWork uow)
    {
        _pedidoRepository = pedidoRepository;
        _movRepository = movRepository;
        _configRepository = configRepository;
        _despesaRepository = despesaRepository;
        _cashbackRepository = cashbackRepository;
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

        // Consideramos como faturamento os pedidos efetivamente Pagos.
        // O dinheiro só entra no sistema se StatusPagamento for Aprovado ou Presencial (entregue no motoqueiro).
        var pedidosValidos = pedidos.Where(p => p.StatusPagamento == StatusPagamento.Aprovado || p.StatusPagamento == StatusPagamento.Presencial).ToList();

        // Filtro por período
        var pedidosHoje = pedidosValidos.Where(p => p.DataHoraPedido.Date == hoje).ToList();
        var pedidosMes = pedidosValidos.Where(p => p.DataHoraPedido.Date >= inicioMes).ToList();

        // Entradas de Estoque (Compras)
        var compras = movimentacoes.Where(m => m.Tipo == TipoMovimentacao.Entrada).ToList();
        var comprasHoje = compras.Where(m => m.DataMovimentacao.Date == hoje).ToList();
        var comprasMes = compras.Where(m => m.DataMovimentacao.Date >= inicioMes).ToList();

        // Despesas (Mão de obra, impostos, etc)
        var despesasObj = await _despesaRepository.GetAllAsync();
        var despesasHoje = despesasObj.Where(d => d.DataRegistro.Date == hoje).ToList();
        var despesasMes = despesasObj.Where(d => d.DataRegistro.Date >= inicioMes).ToList();

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
                    var movsEstoque = await _movRepository.GetAllAsync();
                    var movsNoPeriodo = movsEstoque.Where(m => m.DataMovimentacao >= req.DataInicio && m.DataMovimentacao <= dataFimInclusiva).ToList();
                    foreach (var m in movsNoPeriodo) await _movRepository.DeleteAsync(m);
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
                    // DashboardFinanceiro usa essa chave combinada (Caixa/Estoque e Despesas)
                    var mf = await _movRepository.GetAllAsync();
                    foreach (var m in mf.Where(m => m.DataMovimentacao >= req.DataInicio && m.DataMovimentacao <= dataFimInclusiva).ToList())
                        await _movRepository.DeleteAsync(m);

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
