using System;
using System.Threading.Tasks;

namespace BatatasFritas.Infrastructure.Repositories;

public interface IUnitOfWork : IDisposable
{
    void BeginTransaction();
    Task CommitAsync();
    Task RollbackAsync();
    Task<int> ExecuteRawAsync(string sql, Dictionary<string, object>? parameters = null);
    Task<int> ExecuteHqlAsync(string hql, Dictionary<string, object>? parameters = null);
}
