using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.DTOs;
using NHibernate;
using NHibernate.Linq;

namespace BatatasFritas.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : EntityBase
{
    private readonly ISession _session;

    public Repository(ISession session)
    {
        _session = session;
    }

    public async Task<T?> GetByIdAsync(int id)
    {
        return await _session.GetAsync<T>(id);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _session.Query<T>().ToListAsync();
    }

    public async Task<T?> FindAsync(Expression<Func<T, bool>> predicate) =>
        await _session.Query<T>().Where(predicate).FirstOrDefaultAsync();

    public async Task<IEnumerable<T>> FindManyAsync(Expression<Func<T, bool>> predicate) =>
        await _session.Query<T>().Where(predicate).ToListAsync();

    private const int MaxPageSize = 100;

    public async Task<PagedResult<T>> GetPagedAsync(int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var offset = (page - 1) * pageSize;
        var query  = _session.Query<T>();
        var total  = await query.CountAsync();
        var items  = await query.Skip(offset).Take(pageSize).ToListAsync();
        return new PagedResult<T> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task<PagedResult<T>> GetPagedAsync(Expression<Func<T, bool>> predicate, int page, int pageSize)
    {
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var offset = (page - 1) * pageSize;
        var query  = _session.Query<T>().Where(predicate);
        var total  = await query.CountAsync();
        var items  = await query.Skip(offset).Take(pageSize).ToListAsync();
        return new PagedResult<T> { Items = items, TotalCount = total, Page = page, PageSize = pageSize };
    }

    public async Task AddAsync(T entity)
    {
        await _session.SaveAsync(entity);
    }

    public async Task UpdateAsync(T entity)
    {
        await _session.UpdateAsync(entity);
    }

    public async Task DeleteAsync(T entity)
    {
        await _session.DeleteAsync(entity);
    }
}
