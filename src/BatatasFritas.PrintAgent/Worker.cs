using System.Net.Http.Json;
using BatatasFritas.PrintAgent.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BatatasFritas.PrintAgent;

public class ApiOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string HubUrl  { get; set; } = string.Empty;
}

public class Worker : BackgroundService
{
    private readonly ApiOptions        _api;
    private readonly PrinterService    _printer;
    private readonly ILogger<Worker>   _logger;
    private readonly HttpClient        _http;

    public Worker(
        IOptions<ApiOptions> api,
        PrinterService printer,
        ILogger<Worker> logger)
    {
        _api     = api.Value;
        _printer = printer;
        _logger  = logger;
        _http    = new HttpClient { BaseAddress = new Uri(_api.BaseUrl.TrimEnd('/') + "/") };
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("PrintAgent iniciando. API: {Url}", _api.BaseUrl);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await ConectarEEscutar(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no loop SignalR — tentando novamente em 15s");
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }
        }
    }

    private async Task ConectarEEscutar(CancellationToken ct)
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(_api.HubUrl)
            .WithAutomaticReconnect(new[] { TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30) })
            .Build();

        hub.Closed      += ex => { _logger.LogWarning("SignalR fechado: {Msg}", ex?.Message); return Task.CompletedTask; };
        hub.Reconnecting += ex => { _logger.LogInformation("SignalR reconectando..."); return Task.CompletedTask; };
        hub.Reconnected  += id => { _logger.LogInformation("SignalR reconectado (id={Id})", id); return Task.CompletedTask; };

        hub.On<int>("ImprimirPedido", async pedidoId =>
        {
            _logger.LogInformation("[PRINT] Evento recebido — pedido #{Id}", pedidoId);
            try
            {
                var pedido = await _http.GetFromJsonAsync<PedidoDetalheDto>($"pedidos/{pedidoId}", ct);
                if (pedido == null)
                {
                    _logger.LogWarning("[PRINT] Pedido #{Id} não encontrado", pedidoId);
                    return;
                }
                _printer.Imprimir(pedido);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PRINT] Falha ao processar pedido #{Id}", pedidoId);
            }
        });

        await hub.StartAsync(ct);
        _logger.LogInformation("SignalR conectado em {Url}", _api.HubUrl);

        // Aguarda até cancelamento ou desconexão
        var tcs = new TaskCompletionSource();
        hub.Closed += _ => { tcs.TrySetResult(); return Task.CompletedTask; };
        ct.Register(() => tcs.TrySetResult());

        await tcs.Task;
        await hub.DisposeAsync();
    }

    public override void Dispose()
    {
        _http.Dispose();
        base.Dispose();
    }
}
