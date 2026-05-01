using BatatasFritas.API.Hubs;
using BatatasFritas.Domain.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Testcontainers.PostgreSql;

namespace BatatasFritas.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public IMercadoPagoService MockMercadoPago { get; } = Substitute.For<IMercadoPagoService>();
    public IHubContext<PedidosHub> MockHub { get; } = Substitute.For<IHubContext<PedidosHub>>();

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
        Environment.SetEnvironmentVariable("MercadoPago__WebhookSecret", "test-webhook-secret");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var toRemove = services
                .Where(d =>
                    d.ServiceType == typeof(IMercadoPagoService) ||
                    d.ServiceType == typeof(IHubContext<PedidosHub>))
                .ToList();

            foreach (var d in toRemove)
                services.Remove(d);

            services.AddSingleton(MockMercadoPago);

            var mockClients = Substitute.For<IHubClients>();
            var mockClientProxy = Substitute.For<IClientProxy>();
            MockHub.Clients.Returns(mockClients);
            mockClients.All.Returns(mockClientProxy);
            mockClients.Group(Arg.Any<string>()).Returns(mockClientProxy);
            services.AddSingleton(MockHub);
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}
