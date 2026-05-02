using BatatasFritas.API.Hubs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace BatatasFritas.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public IHubContext<PedidosHub> MockHub { get; } = Substitute.For<IHubContext<PedidosHub>>();
    public IHubClients MockHubClients { get; } = Substitute.For<IHubClients>();
    public IClientProxy MockClientProxy { get; } = Substitute.For<IClientProxy>();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        Environment.SetEnvironmentVariable("Jwt__SecretKey", "test-secret-key-that-is-at-least-32-characters-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "BatatasFritasAPI");
        Environment.SetEnvironmentVariable("Jwt__Audience", "BatatasFritasKDS");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "480");
        Environment.SetEnvironmentVariable("KDS_DEFAULT_PASSWORD", "testpassword123");
        Environment.SetEnvironmentVariable("DatabaseProvider", "postgres");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _db.GetConnectionString());

        // Força startup do servidor (Program.cs + MigrateUp cria tabelas)
        using var warmupClient = CreateClient();
        var _ = await warmupClient.GetAsync("/api/pedidos/bydate?page=1&pageSize=1");

        await SeedAsync();
    }

    private async Task SeedAsync()
    {
        // Program.cs seeds Produtos with estoque=0 during startup.
        // Retry UPDATE until rows exist (seed may not be committed yet when warmup returns).
        await using var conn = new NpgsqlConnection(_db.GetConnectionString());
        await conn.OpenAsync();

        int rows = 0;
        for (int i = 0; i < 20 && rows == 0; i++)
        {
            await Task.Delay(300);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE produtos SET estoque_atual = 100, estoque_minimo = 5
                WHERE nome IN ('Batata Suprema Média', 'Batata Suprema Gigante', 'Coca-Cola 1L');
            ";
            rows = await cmd.ExecuteNonQueryAsync();
        }

        if (rows == 0)
            throw new InvalidOperationException("SeedAsync: produtos não encontrados após 6s. Program.cs seed falhou.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Mock SignalR hub (PedidosController still uses it)
            MockHub.Clients.Returns(MockHubClients);
            MockHubClients.All.Returns(MockClientProxy);
            MockHubClients.Group(Arg.Any<string>()).Returns(MockClientProxy);

            // Remove real hub registration and add mock
            var toRemove = services
                .Where(d => d.ServiceType == typeof(IHubContext<PedidosHub>))
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            services.AddSingleton(MockHub);
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}
