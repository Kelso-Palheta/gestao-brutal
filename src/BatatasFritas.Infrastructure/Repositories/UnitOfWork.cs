using System;
using System.Threading.Tasks;
using NHibernate;

namespace BatatasFritas.Infrastructure.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly ISession _session;
    private ITransaction? _transaction;

    public UnitOfWork(ISession session)
    {
        _session = session;
    }

    public void BeginTransaction()
    {
        _transaction = _session.BeginTransaction();
    }

    public async Task CommitAsync()
    {
        if (_transaction is { IsActive: true })
        {
            await _transaction.CommitAsync();
        }
    }

    public async Task RollbackAsync()
    {
        if (_transaction is { IsActive: true })
        {
            await _transaction.RollbackAsync();
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _session.Dispose();
        GC.SuppressFinalize(this);
    }
}
