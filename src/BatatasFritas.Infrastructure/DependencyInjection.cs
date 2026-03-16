using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Infrastructure.Mappings;
using System;

namespace BatatasFritas.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            Console.WriteLine($"[INFRA] Inicializando Infraestrutura. ConnectionString presente: {!string.IsNullOrEmpty(connectionString)}");

            // Verifica se é PostgreSQL baseado em keywords comuns ou no ambiente de produção
            bool isPostgres = false;
            
            if (!string.IsNullOrEmpty(connectionString))
            {
                isPostgres = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) || 
                             connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase) || 
                             connectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) ||
                             connectionString.Contains("Port=", StringComparison.OrdinalIgnoreCase);
            }

            // Se estiver em produção e não houver indicação clara de SQLite, assume Postgres
            string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) && !isPostgres && !connectionString.Contains("Data Source="))
            {
                Console.WriteLine("[INFRA] Ambiente de Produção detectado. Forçando PostgreSQL.");
                isPostgres = true;
            }

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

            try 
            {
                var sessionFactory = Fluently.Configure()
                    .Database(dbConfig)
                    .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ProdutoMap>())
                    .ExposeConfiguration(cfg => new NHibernate.Tool.hbm2ddl.SchemaUpdate(cfg).Execute(false, true))
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
            
            return services;
        }
    }
}
