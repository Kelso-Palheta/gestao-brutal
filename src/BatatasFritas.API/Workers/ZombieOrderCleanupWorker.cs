using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Shared.Enums;
using Microsoft.AspNetCore.SignalR;

namespace BatatasFritas.API.Workers;

public class ZombieOrderCleanupWorker : BackgroundService
{
    private static readonly TimeSpan Interval   = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ZombieAge  = TimeSpan.FromMinutes(30);

    private readonly IServiceProvider _services;
    private readonly ILogger<ZombieOrderCleanupWorker> _logger;

    public ZombieOrderCleanupWorker(IServiceProvider services, ILogger<ZombieOrderCleanupWorker> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CancelarZombiesAsync(stoppingToken);
        }
    }

    private async Task CancelarZombiesAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();

        var repo = scope.ServiceProvider.GetRequiredService<IRepository<Pedido>>();
        var uow  = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var hub  = scope.ServiceProvider.GetRequiredService<IHubContext<PedidosHub>>();

        var limite = DateTime.UtcNow - ZombieAge;

        var zombies = await repo.FindManyAsync(p =>
            p.StatusPagamento == StatusPagamento.Pendente &&
            (p.Status == StatusPedido.Recebido || p.Status == StatusPedido.Aceito) &&
            p.DataHoraPedido < limite);

        if (!zombies.Any()) return;

        uow.BeginTransaction();
        try
        {
            foreach (var pedido in zombies)
            {
                pedido.AlterarStatus(StatusPedido.Cancelado);
                pedido.StatusPagamento = StatusPagamento.Cancelado;
                pedido.Observacao      = "Cancelado automaticamente: PIX não confirmado em 30 minutos.";
                await repo.UpdateAsync(pedido);

                _logger.LogInformation("Zombie cancelado: Pedido #{Id} ({Cliente})", pedido.Id, pedido.NomeCliente);
            }

            await uow.CommitAsync();

            foreach (var pedido in zombies)
                await hub.Clients.All.SendAsync("StatusAtualizado", pedido.Id, "Cancelado", ct);
        }
        catch (Exception ex)
        {
            await uow.RollbackAsync();
            _logger.LogError(ex, "Erro ao cancelar pedidos zumbi");
        }
    }
}
