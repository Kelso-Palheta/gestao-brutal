using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using BatatasFritas.Domain.Entities;

namespace BatatasFritas.Infrastructure.Repositories;

public interface IRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();

    /// <summary>Retorna o primeiro registro que satisfaz o predicado ou null. Query filtrada no banco.</summary>
    Task<T?> FindAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
