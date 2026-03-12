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
            var sessionFactory = Fluently.Configure()
                .Database(SQLiteConfiguration.Standard.ConnectionString(connectionString))
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
