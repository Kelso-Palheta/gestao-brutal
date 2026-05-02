using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class KdsController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IRepository<CarteiraCashback> _carteiraRepository;
    private readonly IRepository<Configuracao> _configRepository;
    private readonly IUnitOfWork _uow;
    private readonly IHubContext<PedidosHub> _hub;

    public KdsController(
        IRepository<Pedido> pedidoRepository,
        IRepository<CarteiraCashback> carteiraRepository,
        IRepository<Configuracao> configRepository,
        IUnitOfWork uow,
        IHubContext<PedidosHub> hub)
    {
        _pedidoRepository   = pedidoRepository;
        _carteiraRepository = carteiraRepository;
        _configRepository   = configRepository;
        _uow                = uow;
        _hub                = hub;
    }

    [HttpGet("ativos")]
    public async Task<IActionResult> GetPedidosAtivos()
    {
        // FindManyAsync filtra no banco — evita full table scan
        var pedidos = await _pedidoRepository.FindManyAsync(p =>
            p.Status != StatusPedido.Cancelado &&
            (p.Status != StatusPedido.Entregue ||
             (p.Status == StatusPedido.Entregue &&
              p.StatusPagamento != StatusPagamento.Aprovado &&
              p.StatusPagamento != StatusPagamento.Presencial)));

        var ativos = pedidos
            .OrderBy(p => p.DataHoraPedido)
            .Select(p => 
            {
                var itens = p.Itens.Select(i => new ItemPedidoDetalheDto
                {
                    Id = i.Id,
                    NomeProduto = i.Produto.Nome,
                    Quantidade = i.Quantidade,
                    Observacao = i.Observacao
                }).ToList();

                return new PedidoDetalheDto
                {
                    Id                 = p.Id,
                    NomeCliente        = p.NomeCliente,
                    TelefoneCliente    = p.TelefoneCliente,
                    EnderecoEntrega    = p.EnderecoEntrega,
                    NomeBairro         = p.BairroEntrega?.Nome ?? "",
                    MetodoPagamento    = p.MetodoPagamento,
                    StatusPagamento    = p.StatusPagamento,
                    TipoAtendimento    = p.TipoAtendimento,
                    TrocoPara          = p.TrocoPara,
                    SubtotalItens      = p.Itens.Sum(i => i.PrecoUnitario * i.Quantidade),
                    TaxaEntrega        = p.BairroEntrega?.TaxaEntrega ?? 0m,
                    ValorCashbackUsado = p.ValorCashbackUsado,
                    ValorTotal         = p.ValorTotal,
                    Status             = p.Status,
                    DataHoraPedido     = p.DataHoraPedido,
                    Itens              = itens
                };
            }).ToList();

        return Ok(ativos);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(int id, [FromBody] StatusPedido novoStatus)
    {
        var pedido = await _pedidoRepository.GetByIdAsync(id);
        if (pedido == null) return NotFound();

        try
        {
            _uow.BeginTransaction();

            // Se mudou para Entregue, credita Cashback (se configurado)
            if (novoStatus == StatusPedido.Entregue && pedido.Status != StatusPedido.Entregue
                && !string.IsNullOrWhiteSpace(pedido.TelefoneCliente))
            {
                var telLimpo = new string(pedido.TelefoneCliente.Where(char.IsDigit).ToArray());

                // FindAsync → query filtrada direto no banco
                var configCb = await _configRepository.FindAsync(c => c.Chave == "cashback_porcentagem");

                if (configCb != null && decimal.TryParse(configCb.Valor, out var porcentagem) && porcentagem > 0)
                {
                    var carteira = await _carteiraRepository.FindAsync(c => c.Telefone == telLimpo);

                    if (carteira == null)
                    {
                        carteira = new CarteiraCashback(telLimpo, pedido.NomeCliente);
                        await _carteiraRepository.AddAsync(carteira);
                    }

                    var valorGanho = Math.Round(pedido.ValorElegivelCashback * (porcentagem / 100m), 2);
                    if (valorGanho > 0)
                    {
                        carteira.AdicionarSaldo(valorGanho, $"Ganho de {porcentagem}% no pedido #{pedido.Id}", pedido.Id);
                        await _carteiraRepository.UpdateAsync(carteira);
                    }
                }
            }

            pedido.AlterarStatus(novoStatus);
            await _pedidoRepository.UpdateAsync(pedido);
            await _uow.CommitAsync();

            // Notifica todos os clientes conectados que houve mudança de status
            await _hub.Clients.All.SendAsync("StatusAtualizado", id, novoStatus.ToString());

            return NoContent();
        }
        catch (Exception ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// PUT api/kds/{id}/pagar
    /// Marca um pedido como pago. ComprovantePix opcional: E2E ID do PIX para prevenção de fraude.
    /// </summary>
    [HttpPut("{id}/pagar")]
    public async Task<IActionResult> MarcarComoPago(int id, [FromBody] AprovarPagamentoDto? dto = null)
    {
        var pedido = await _pedidoRepository.GetByIdAsync(id);
        if (pedido == null) return NotFound();

        var comprovantePix = dto?.ComprovantePix?.Trim();

        if (!string.IsNullOrEmpty(comprovantePix))
        {
            var duplicado = await _pedidoRepository.FindManyAsync(
                p => p.ComprovantePix == comprovantePix && p.Id != id);
            if (duplicado.Any())
                return BadRequest("Comprovante PIX já utilizado em outro pedido.");
        }

        _uow.BeginTransaction();
        try
        {
            pedido.AprovarPagamentoManual(comprovantePix);
            await _pedidoRepository.UpdateAsync(pedido);
            await _uow.CommitAsync();
        }
        catch (InvalidOperationException ex)
        {
            await _uow.RollbackAsync();
            return BadRequest(ex.Message);
        }

        await _hub.Clients.All.SendAsync("StatusAtualizado", id, "Pago");

        return NoContent();
    }

    /// <summary>
    /// PUT api/kds/{id}/desfazer-pagamento
    /// Desfaz o pagamento, voltando o pedido para pendente.
    /// </summary>
    [HttpPut("{id}/desfazer-pagamento")]
    public async Task<IActionResult> DesfazerPagamento(int id)
    {
        var pedido = await _pedidoRepository.GetByIdAsync(id);
        if (pedido == null) return NotFound();

        _uow.BeginTransaction();
        pedido.StatusPagamento = StatusPagamento.Pendente;
        await _pedidoRepository.UpdateAsync(pedido);
        await _uow.CommitAsync();

        await _hub.Clients.All.SendAsync("StatusAtualizado", id, "PagamentoPendente");

        return NoContent();
    }

    /// <summary>
    /// POST api/kds/{id}/cancelar
    /// Cancela um pedido e grava o motivo informado pelo operador KDS.
    /// Body: { "motivo": "Cliente desistiu" }
    /// </summary>
    [HttpPost("{id}/cancelar")]
    public async Task<IActionResult> CancelarPedido(int id, [FromBody] CancelarPedidoRequest request)
    {
        var pedido = await _pedidoRepository.GetByIdAsync(id);
        if (pedido == null) return NotFound();

        if (pedido.Status == StatusPedido.Entregue)
            return BadRequest("Não é possível cancelar um pedido já entregue.");

        _uow.BeginTransaction();
        pedido.AlterarStatus(StatusPedido.Cancelado);

        // Grava o motivo no campo de observação do pedido (usado para rastreio)
        if (!string.IsNullOrWhiteSpace(request?.Motivo))
            pedido.Observacao = $"[CANCELADO] {request.Motivo.Trim()}";

        await _pedidoRepository.UpdateAsync(pedido);
        await _uow.CommitAsync();

        return NoContent();
    }
}

/// <summary>DTO mínimo para o body do cancelamento.</summary>
public record CancelarPedidoRequest(string? Motivo);
