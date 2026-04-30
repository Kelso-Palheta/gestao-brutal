using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.DTOs;
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
        private const int MaxPageSize = 100;

        private readonly ISession _session;

        public NHibernateRepository(ISession session) => _session = session;

        public async Task<T?> GetByIdAsync(int id) =>
            await _session.GetAsync<T>(id);

        public async Task<IEnumerable<T>> GetAllAsync() =>
            await _session.Query<T>().ToListAsync();

        public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate) =>
            await _session.Query<T>().Where(predicate).FirstOrDefaultAsync();

        public async Task<IEnumerable<T>> FindManyAsync(Expression<Func<T, bool>> predicate) =>
            await _session.Query<T>().Where(predicate).ToListAsync();

        public async Task<PagedResult<T>> GetPagedAsync(int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var offset = (page - 1) * pageSize;

            var query      = _session.Query<T>();
            var totalCount = await query.CountAsync();
            var items      = await query.Skip(offset).Take(pageSize).ToListAsync();

            return new PagedResult<T>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public async Task<PagedResult<T>> GetPagedAsync(Expression<Func<T, bool>> predicate, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
            var offset = (page - 1) * pageSize;

            var query      = _session.Query<T>().Where(predicate);
            var totalCount = await query.CountAsync();
            var items      = await query.Skip(offset).Take(pageSize).ToListAsync();

            return new PagedResult<T>
            {
                Items      = items,
                TotalCount = totalCount,
                Page       = page,
                PageSize   = pageSize
            };
        }

        public async Task AddAsync(T entity)    => await _session.SaveAsync(entity);
        public async Task UpdateAsync(T entity) => await _session.UpdateAsync(entity);
        public async Task DeleteAsync(T entity) => await _session.DeleteAsync(entity);
    }
}
