using BatatasFritas.Domain.Entities;
using NHibernate;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace BatatasFritas.Infrastructure.Repositories
{
    public class NHibernateRepository<T> : IRepository<T> where T : EntityBase
    {
        private readonly ISession _session;

        public NHibernateRepository(ISession session) => _session = session;

        public async Task<T?> GetByIdAsync(int id) =>
            await _session.GetAsync<T>(id);

        public async Task<IEnumerable<T>> GetAllAsync() =>
            await _session.Query<T>().ToListAsync();

        public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate) =>
            await _session.Query<T>().Where(predicate).FirstOrDefaultAsync();

        public async Task AddAsync(T entity)    => await _session.SaveAsync(entity);
        public async Task UpdateAsync(T entity) => await _session.UpdateAsync(entity);
        public async Task DeleteAsync(T entity) => await _session.DeleteAsync(entity);
    }
}
