using System.Data.Common;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;

namespace BatatasFritas.API.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _sharedConnection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DatabaseProvider", "sqlite" },
                { "ConnectionStrings:DefaultConnection", "Data Source=:memory:;Cache=Shared" },
                { "Jwt:SecretKey", "test-secret-key-that-is-at-least-32-characters-long" },
                { "Jwt:Issuer", "BatatasFritasAPI" },
                { "Jwt:Audience", "BatatasFritasKDS" },
                { "Jwt:ExpirationMinutes", "480" },
                { "KDS_DEFAULT_PASSWORD", "testpassword123" },
                { "MercadoPago:WebhookSecret", "test-webhook-secret" }
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove existing NHibernate services so they can be re-added
            var descriptorsToRemove = services.Where(d =>
                d.ServiceType == typeof(ISessionFactory) ||
                d.ServiceType == typeof(ISession) ||
                d.ServiceType == typeof(IMigrationRunner)
            ).ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);
        });

        // Keep a shared connection open to preserve the in-memory database
        _sharedConnection = new SqliteConnection("Data Source=:memory:;Cache=Shared");
        _sharedConnection.Open();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _sharedConnection?.Dispose();
    }
}
