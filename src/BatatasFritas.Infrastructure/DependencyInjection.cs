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
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string databaseProvider = "sqlite", IConfiguration? configuration = null)
        {
            Console.WriteLine($"[INFRA] Inicializando Infraestrutura. Provider: {databaseProvider}");

            bool isPostgres = databaseProvider.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                           || databaseProvider.Equals("postgresql", StringComparison.OrdinalIgnoreCase);

            IPersistenceConfigurer dbConfig;

            if (isPostgres)
            {
                Console.WriteLine("[INFRA] Usando configuração de banco: PostgreSQL (Npgsql)");
                dbConfig = PostgreSQLConfiguration.PostgreSQL82
                    .ConnectionString(connectionString);
            }
            else
            {
                Console.WriteLine("[INFRA] Usando configuração de banco: SQLite");
                dbConfig = SQLiteConfiguration.Standard.ConnectionString(connectionString);
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

                Console.WriteLine("[INFRA] SessionFactory criado com sucesso.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INFRA] ERRO FATAL ao criar SessionFactory: {ex.Message}");
                if (ex.InnerException != null) Console.WriteLine($"[INFRA] Inner Exception: {ex.InnerException.Message}");
                throw;
            }

            // ── Mercado Pago ──────────────────────────────────────────────────
            if (configuration != null)
                services.Configure<MercadoPagoOptions>(configuration.GetSection("MercadoPago"));
            else
                services.Configure<MercadoPagoOptions>(_ => { });

            services.AddHttpClient(); // necessário para IHttpClientFactory (Point Smart 2)
            services.AddScoped<IMercadoPagoService, MercadoPagoService>();

            return services;
        }
    }
}
