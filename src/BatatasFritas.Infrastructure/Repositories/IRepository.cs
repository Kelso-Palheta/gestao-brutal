using System.Collections.Generic;
using System.Threading.Tasks;
using BatatasFritas.Domain.Entities;

namespace BatatasFritas.Infrastructure.Repositories;

public interface IRepository<T> where T : EntityBase
{
    Task<T?> GetByIdAsync(int id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
}
