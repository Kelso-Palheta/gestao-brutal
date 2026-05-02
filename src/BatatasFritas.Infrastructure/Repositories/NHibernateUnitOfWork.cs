using NHibernate;
using System.Collections.Generic;
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

        public async Task<int> ExecuteRawAsync(string sql, Dictionary<string, object>? parameters = null)
        {
            var query = _session.CreateSQLQuery(sql);
            if (parameters != null)
                foreach (var (key, value) in parameters)
                    query.SetParameter(key, value);
            return await query.ExecuteUpdateAsync();
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _session.Dispose();
        }
    }
}
