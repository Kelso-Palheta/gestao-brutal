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
            cartaoPeriodo = pPeriodo.Where(p => p.MetodoPagamento == MetodoPagamento.Cartao).Sum(p => p.ValorTotal);
            dinheiroPeriodo = pPeriodo.Where(p => p.MetodoPagamento == MetodoPagamento.Dinheiro).Sum(p => p.ValorTotal);
        }

        // Leitura de Meta Diária
        decimal metaDiaria = 0m;
        var configMeta = configs.FirstOrDefault(c => c.Chave == "meta_diaria_vendas");
        if (configMeta != null && decimal.TryParse(configMeta.Valor, out var metaObj))
        {
            metaDiaria = metaObj;
        }

        // --- DETALHES DO MÊS ---
        var pixMes      = pedidosMes.Where(p => p.MetodoPagamento == MetodoPagamento.Pix).Sum(p => p.ValorTotal);
        var cartaoMes   = pedidosMes.Where(p => p.MetodoPagamento == MetodoPagamento.Cartao).Sum(p => p.ValorTotal);
        var dinheiroMes = pedidosMes.Where(p => p.MetodoPagamento == MetodoPagamento.Dinheiro).Sum(p => p.ValorTotal);

        var despFuncMes    = despesasMes.Where(d => d.Categoria == "Funcionario").Sum(d => d.Valor);
        var despEnergMes   = despesasMes.Where(d => d.Categoria == "Energia/Agua").Sum(d => d.Valor);
        var despImpostoMes = despesasMes.Where(d => d.Categoria == "Imposto").Sum(d => d.Valor);
        var despOutrosMes  = despesasMes.Where(d => d.Categoria == "Outros").Sum(d => d.Valor);

        // --- DETALHES DO PERÍODO ---
        var despFuncPer    = 0m;
        var despEnergPer   = 0m;
        var despImpostoPer = 0m;
        var despOutrosPer  = 0m;

        if (inicio.HasValue && fim.HasValue)
        {
            var dataFimInclusiva2 = fim.Value.Date.AddDays(1).AddTicks(-1);
            var dPer = despesasObj.Where(d => TimeZoneInfo.ConvertTimeFromUtc(d.DataRegistro, tz) >= inicio.Value && TimeZoneInfo.ConvertTimeFromUtc(d.DataRegistro, tz) <= dataFimInclusiva2).ToList();
            despFuncPer    = dPer.Where(d => d.Categoria == "Funcionario").Sum(d => d.Valor);
            despEnergPer   = dPer.Where(d => d.Categoria == "Energia/Agua").Sum(d => d.Valor);
            despImpostoPer = dPer.Where(d => d.Categoria == "Imposto").Sum(d => d.Valor);
            despOutrosPer  = dPer.Where(d => d.Categoria == "Outros").Sum(d => d.Valor);
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
            PixHoje     = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Pix).Sum(p => p.ValorTotal),
            CartaoHoje  = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Cartao).Sum(p => p.ValorTotal),
            DinheiroHoje = pedidosHoje.Where(p => p.MetodoPagamento == MetodoPagamento.Dinheiro).Sum(p => p.ValorTotal),

            MetaDiaria = metaDiaria,

            // Métodos Mês
            PixMes      = pixMes,
            CartaoMes   = cartaoMes,
            DinheiroMes = dinheiroMes,

            // Despesas Mês por Categoria
            DespesaFuncionarioMes  = despFuncMes,
            DespesaEnergiaAguaMes  = despEnergMes,
            DespesaImpostoMes      = despImpostoMes,
            DespesaOutrosMes       = despOutrosMes,

            // Métricas Período
            VendasPeriodo      = vendasPeriodo,
            ComprasPeriodo     = comprasPeriodo,
            DespesasPeriodo    = despesasPeriodo,
            TotalPedidosPeriodo = totalPedidosPeriodo,
            PixPeriodo         = pixPeriodo,
            CartaoPeriodo      = cartaoPeriodo,
            DinheiroPeriodo    = dinheiroPeriodo,

            // Despesas Período por Categoria
            DespesaFuncionarioPeriodo  = despFuncPer,
            DespesaEnergiaAguaPeriodo  = despEnergPer,
            DespesaImpostoPeriodo      = despImpostoPer,
            DespesaOutrosPeriodo       = despOutrosPer,

            DataInicioFiltro = inicio,
            DataFimFiltro    = fim
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

    /// <summary>
    /// Endpoint para o Dashboard de Analytics Financeiro com dados separados por categoria.
    /// Card Dourado (Batatas), Card Azul (Bebidas), Card Verde (Entregas).
    /// </summary>
    [HttpGet("analytics")]
    public async Task<IActionResult> GetDashboardAnalytics([FromQuery] DateTime? inicio, [FromQuery] DateTime? fim)
    {
        var tzBrasilia = TimeZoneInfo.CreateCustomTimeZone("BRT", TimeSpan.FromHours(-3), "BRT", "BRT");
        var agora = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tzBrasilia);
        var hoje = agora.Date;

        // Define o período: usa filtro ou hoje por padrão
        var dataInicio = inicio ?? hoje;
        var dataFim = fim ?? hoje;
        var dataFimInclusiva = dataFim.Date.AddDays(1).AddTicks(-1);

        // Busca apenas pedidos pagos (faturamento real)
        var pedidos = (await _pedidoRepository.GetAllAsync())
            .Where(p => p.StatusPagamento == StatusPagamento.Aprovado || p.StatusPagamento == StatusPagamento.Presencial)
            .ToList();

        // Filtra pelo período
        var pedidosPeriodo = pedidos
            .Where(p => {
                var d = TimeZoneInfo.ConvertTimeFromUtc(p.DataHoraPedido, tzBrasilia);
                return d >= dataInicio && d <= dataFimInclusiva;
            })
            .ToList();

        // === QUERY LINQ: Soma separada de Batatas e Bebidas ===
        // Teste de validação: soma itens por categoria de produto
        var itensBatatas = pedidosPeriodo
            .SelectMany(p => p.Itens)
            .Where(i => i.Produto.CategoriaId == CategoriaEnum.Batatas)
            .ToList();

        var itensBebidas = pedidosPeriodo
            .SelectMany(p => p.Itens)
            .Where(i => i.Produto.CategoriaId == CategoriaEnum.Bebidas)
            .ToList();

        // Soma de batatas: precoUnitario * quantidade
        var receitaBatatas = itensBatatas.Sum(i => i.PrecoUnitario * i.Quantidade);
        var qtdBatatasVendidas = itensBatatas.Sum(i => i.Quantidade);

        // Soma de bebidas: precoUnitario * quantidade
        var receitaBebidas = itensBebidas.Sum(i => i.PrecoUnitario * i.Quantidade);
        var qtdBebidasVendidas = itensBebidas.Sum(i => i.Quantidade);

        // Cashback: 5% sobre o valor das batatas (apenas batatas geram cashback)
        var cashbackGeradoBatatas = receitaBatatas * 0.05m;

        // Entregas: pedidos com bairro (delivery)
        var pedidosDelivery = pedidosPeriodo
            .Where(p => p.BairroEntrega != null)
            .ToList();

        var receitaEntregas = pedidosDelivery.Sum(p => p.TaxaEntrega);
        var totalEntregas = pedidosDelivery.Count;
        var taxaEntregaMedia = totalEntregas > 0 ? receitaEntregas / totalEntregas : 0m;

        // Lista de pedidos finalizados para o rodapé
        var pedidosFinalizados = pedidosPeriodo
            .OrderByDescending(p => p.DataHoraPedido)
            .Select(p => new PedidoResumoDto
            {
                Id = p.Id,
                NomeCliente = p.NomeCliente,
                DataHoraPedido = TimeZoneInfo.ConvertTimeFromUtc(p.DataHoraPedido, tzBrasilia),
                Bairro = p.BairroEntrega?.Nome ?? "Retirada",
                ValorBatatas = p.Itens
                    .Where(i => i.Produto.CategoriaId == CategoriaEnum.Batatas)
                    .Sum(i => i.PrecoUnitario * i.Quantidade),
                ValorBebidas = p.Itens
                    .Where(i => i.Produto.CategoriaId == CategoriaEnum.Bebidas)
                    .Sum(i => i.PrecoUnitario * i.Quantidade),
                TaxaEntrega = p.TaxaEntrega,
                ValorTotal = p.ValorTotal,
                QtdItens = p.Itens.Sum(i => i.Quantidade)
            })
            .ToList();

        var dto = new DashboardAnalyticsDto
        {
            // Card Dourado - Batatas
            ReceitaBatatas = receitaBatatas,
            QuantidadeBatatasVendidas = qtdBatatasVendidas,
            CashbackGeradoBatatas = cashbackGeradoBatatas,

            // Card Azul - Bebidas
            ReceitaBebidas = receitaBebidas,
            QuantidadeBebidasVendidas = qtdBebidasVendidas,

            // Card Verde - Entregas
            ReceitaEntregas = receitaEntregas,
            TotalEntregas = totalEntregas,
            TaxaEntregaMedia = taxaEntregaMedia,

            // Resumo Geral
            TotalPedidosFinalizados = pedidosPeriodo.Count,

            // Período
            DataInicio = dataInicio,
            DataFim = dataFim,

            // Lista de Pedidos
            PedidosFinalizados = pedidosFinalizados
        };

        return Ok(dto);
    }
}
