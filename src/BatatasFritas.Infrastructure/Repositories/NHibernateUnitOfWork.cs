using NHibernate;
using System.Threading.Tasks;

namespace BatatasFritas.Infrastructure.Repositories
{
    public class NHibernateUnitOfWork : IUnitOfWork
    {
        private readonly ISession _session;
        private ITransaction? _transaction;

        public NHibernateUnitOfWork(ISession session)
        {
            _session = session;
        }

        public void BeginTransaction()
        {
            _transaction = _session.BeginTransaction();
        }

        public async Task CommitAsync()
        {
            if (_transaction != null && _transaction.IsActive)
            {
                await _transaction.CommitAsync();
            }
        }

        public async Task RollbackAsync()
        {
            if (_transaction != null && _transaction.IsActive)
            {
                await _transaction.RollbackAsync();
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _session.Dispose();
        }
    }
}
