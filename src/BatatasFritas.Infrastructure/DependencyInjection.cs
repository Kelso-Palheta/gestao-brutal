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
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString, string databaseProvider = "sqlite")
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
