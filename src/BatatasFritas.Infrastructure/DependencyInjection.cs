using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using BatatasFritas.Infrastructure.Repositories;
using BatatasFritas.Infrastructure.Mappings;

namespace BatatasFritas.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            // Detecta automaticamente se é PostgreSQL (produção) ou SQLite (desenvolvimento)
            IPersistenceConfigurer dbConfig = connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase)
                ? PostgreSQLConfiguration.PostgreSQL83.ConnectionString(connectionString)
                : (IPersistenceConfigurer)SQLiteConfiguration.Standard.ConnectionString(connectionString);

            var sessionFactory = Fluently.Configure()
                .Database(dbConfig)
                .Mappings(m => m.FluentMappings.AddFromAssemblyOf<ProdutoMap>())
                .ExposeConfiguration(cfg => new NHibernate.Tool.hbm2ddl.SchemaUpdate(cfg).Execute(false, true))
                .BuildSessionFactory();

            services.AddSingleton(sessionFactory);
            services.AddScoped(factory => sessionFactory.OpenSession());
            services.AddScoped(typeof(IRepository<>), typeof(NHibernateRepository<>));
            services.AddScoped<IUnitOfWork, NHibernateUnitOfWork>();
            
            return services;
        }
    }
}
