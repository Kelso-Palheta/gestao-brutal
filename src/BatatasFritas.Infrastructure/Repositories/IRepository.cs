using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.DTOs;

namespace BatatasFritas.Infrastructure.Repositories;

public interface IRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>Retorna o primeiro registro que satisfaz o predicado ou null. Query filtrada no banco.</summary>
    Task<T?> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>Retorna todos os registros que satisfazem o predicado. Query filtrada no banco — não carrega tudo na memória.</summary>
    Task<IEnumerable<T>> FindManyAsync(Expression<Func<T, bool>> predicate);

    /// <summary>Retorna página de registros. page base 1; pageSize máx 100.</summary>
    Task<PagedResult<T>> GetPagedAsync(int page, int pageSize);

    /// <summary>Retorna página filtrada de registros. page base 1; pageSize máx 100.</summary>
    Task<PagedResult<T>> GetPagedAsync(Expression<Func<T, bool>> predicate, int page, int pageSize);

    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
