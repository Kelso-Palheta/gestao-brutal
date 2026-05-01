using FluentMigrator.Runner;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NHibernate;
using BatatasFritas.Domain.Interfaces;
using BatatasFritas.Infrastructure.Options;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Infrastructure.Mappings;
using BatatasFritas.Infrastructure.Migrations;
using BatatasFritas.Infrastructure.Services;
using System;

namespace BatatasFritas.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string databaseProvider = "sqlite", IConfiguration? configuration = null, Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory = null)
        {
            var log = loggerFactory?.CreateLogger("Infrastructure");
            log?.LogInformation("Inicializando Infraestrutura. Provider: {Provider}", databaseProvider);

            bool isPostgres = databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                           || databaseProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase);

            IPersistenceConfigurer dbConfig;

            if (isPostgres)
            {
                log?.LogInformation("Usando configuração de banco: PostgreSQL (Npgsql)");
                dbConfig = PostgreSQLConfiguration.PostgreSQL82
                    .ConnectionString(connectionString);
            }
            else
            {
                log?.LogInformation("Usando configuração de banco: SQLite");
                dbConfig = SQLiteConfiguration.Standard
                    .ConnectionString(connectionString);
            }

            // ── FluentMigrator ────────────────────────────────────────────────
            services.AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    if (isPostgres)
                        rb.AddPostgres();
                    else
                        rb.AddSQLite();

                    rb.WithGlobalConnectionString(connectionString)
                      .ScanIn(typeof(V001__CreateBairros).Assembly).For.Migrations();
                })
                .AddLogging(lb => lb.AddConsole());

            // ── NHibernate (sem SchemaUpdate) ─────────────────────────────────
            try
            {
                var sessionFactory = Fluently.Configure()
                    .Database(dbConfig)
                    .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ProdutoMap>())
                    .BuildSessionFactory();

                services.AddSingleton(sessionFactory);
                services.AddScoped(factory => sessionFactory.OpenSession());
                services.AddScoped(typeof(IRepository<>), typeof(NHibernateRepository<>));
                services.AddScoped<IUnitOfWork, NHibernateUnitOfWork>();

                log?.LogInformation("SessionFactory criado com sucesso.");
            }
            catch (Exception ex)
            {
                log?.LogError(ex, "ERRO FATAL ao criar SessionFactory: {Message}", ex.Message);
                if (ex.InnerException != null) log?.LogError("Inner Exception: {InnerMessage}", ex.InnerException.Message);
                throw;
            }

            // ── Mercado Pago ──────────────────────────────────────────────────
            if (configuration != null)
                services.Configure<MercadoPagoOptions>(configuration.GetSection("MercadoPago"));
            else
                services.Configure<MercadoPagoOptions>(_ => { });

            // Cliente nomeado com Polly retry (3x, exponential backoff) para MP Point Smart 2
            services.AddHttpClient("MercadoPagoPoint")
                    .AddStandardResilienceHandler();
            services.AddScoped<IMercadoPagoService, MercadoPagoService>();

            return services;
        }
    }
}
