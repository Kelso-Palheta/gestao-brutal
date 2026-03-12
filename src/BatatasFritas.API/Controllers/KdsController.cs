using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.DTOs;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace BatatasFritas.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KdsController : ControllerBase
{
    private readonly IRepository<Pedido> _pedidoRepository;
    private readonly IRepository<CarteiraCashback> _carteiraRepository;
    private readonly IRepository<Configuracao> _configRepository;
    private readonly IUnitOfWork _uow;

    public KdsController(
        IRepository<Pedido> pedidoRepository,
        IRepository<CarteiraCashback> carteiraRepository,
        IRepository<Configuracao> configRepository,
        IUnitOfWork uow)
    {
        _pedidoRepository = pedidoRepository;
        _carteiraRepository = carteiraRepository;
        _configRepository = configRepository;
        _uow = uow;
    }

    [HttpGet("ativos")]
    public async Task<IActionResult> GetPedidosAtivos()
    {
        var pedidos = await _pedidoRepository.GetAllAsync();
        
        var ativos = pedidos
            .Where(p => p.Status != StatusPedido.Cancelado && p.Status != StatusPedido.Entregue)
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

                // Forçar a soma matemática acessando a coleção e contornando bugs de proxy nulo do ORM
                var somaItens = p.Itens.Sum(i => i.PrecoUnitario * i.Quantidade);
                var somaTaxa = p.BairroEntrega?.TaxaEntrega ?? 0;
                var total = somaItens + somaTaxa;

                return new PedidoDetalheDto
                {
                    Id = p.Id,
                    NomeCliente = p.NomeCliente,
                    TelefoneCliente = p.TelefoneCliente,
                    EnderecoEntrega = p.EnderecoEntrega,
                    NomeBairro = p.BairroEntrega != null ? p.BairroEntrega.Nome : "",
                    MetodoPagamento = p.MetodoPagamento,
                    TrocoPara = p.TrocoPara,
                    ValorTotal = total,
                    Status = p.Status,
                    DataHoraPedido = p.DataHoraPedido,
                    Itens = itens
                };
            }).ToList();

        return Ok(ativos);
    }

    [HttpPut("{id}/status")]
    public async Task<IActionResult> AtualizarStatus(int id, [FromBody] StatusPedido novoStatus)
    {
        var pedido = await _pedidoRepository.GetByIdAsync(id);
        if (pedido == null) return NotFound();

        _uow.BeginTransaction();
        
        // Se mudou para Entregue, vamos dar o Cashback (se configurado)
        if (novoStatus == StatusPedido.Entregue && pedido.Status != StatusPedido.Entregue)
        {
            if (!string.IsNullOrWhiteSpace(pedido.TelefoneCliente))
            {
                var telLimpo = new string(pedido.TelefoneCliente.Where(char.IsDigit).ToArray());
                
                var configs = await _configRepository.GetAllAsync();
                var configCb = configs.FirstOrDefault(c => c.Chave == "cashback_porcentagem");
                if (configCb != null && decimal.TryParse(configCb.Valor, out var porcentagem) && porcentagem > 0)
                {
                    var carteiras = await _carteiraRepository.GetAllAsync();
                    var carteira = carteiras.FirstOrDefault(c => c.Telefone == telLimpo);
                    
                    if (carteira == null)
                    {
                        carteira = new CarteiraCashback(telLimpo, pedido.NomeCliente);
                        await _carteiraRepository.AddAsync(carteira);
                    }

                    // Calcula o ganho em cima do TotalPago (ValorTotal)
                    var valorGanho = pedido.ValorTotal * (porcentagem / 100m);
                    
                    if (valorGanho > 0)
                    {
                        carteira.AdicionarSaldo(Math.Round(valorGanho, 2), $"Ganho de {porcentagem}% no pedido #{pedido.Id}", pedido.Id);
                        await _carteiraRepository.UpdateAsync(carteira);
                    }
                }
            }
        }

        pedido.AlterarStatus(novoStatus);
        await _pedidoRepository.UpdateAsync(pedido);
        await _uow.CommitAsync();

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
