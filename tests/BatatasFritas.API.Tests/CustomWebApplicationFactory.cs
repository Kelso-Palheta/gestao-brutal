using BatatasFritas.Domain.Interfaces;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NSubstitute;

namespace BatatasFritas.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _sharedConnection;
    public IMercadoPagoService MockMercadoPago { get; } = Substitute.For<IMercadoPagoService>();

    public CustomWebApplicationFactory()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"batatastestes_{Guid.NewGuid():N}.db");

        Environment.SetEnvironmentVariable("Jwt__SecretKey", "test-secret-key-that-is-at-least-32-characters-long");
        Environment.SetEnvironmentVariable("Jwt__Issuer", "BatatasFritasAPI");
        Environment.SetEnvironmentVariable("Jwt__Audience", "BatatasFritasKDS");
        Environment.SetEnvironmentVariable("Jwt__ExpirationMinutes", "480");
        Environment.SetEnvironmentVariable("KDS_DEFAULT_PASSWORD", "testpassword123");
        Environment.SetEnvironmentVariable("DatabaseProvider", "sqlite");
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $"Data Source={dbFile};Cache=Shared");
        Environment.SetEnvironmentVariable("MercadoPago__WebhookSecret", "test-webhook-secret");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(ISessionFactory) ||
                d.ServiceType == typeof(ISession) ||
                d.ServiceType == typeof(IMigrationRunner) ||
                d.ServiceType == typeof(IMercadoPagoService)
            ).ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            services.AddSingleton(MockMercadoPago);
        });

        _sharedConnection = new SqliteConnection(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));
        _sharedConnection.Open();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _sharedConnection?.Dispose();
    }
}
