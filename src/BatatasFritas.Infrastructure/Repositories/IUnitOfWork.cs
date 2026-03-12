using System;
using System.Threading.Tasks;

namespace BatatasFritas.Infrastructure.Repositories;

public interface IUnitOfWork : IDisposable
{
    void BeginTransaction();
    Task CommitAsync();
    Task RollbackAsync();
}
