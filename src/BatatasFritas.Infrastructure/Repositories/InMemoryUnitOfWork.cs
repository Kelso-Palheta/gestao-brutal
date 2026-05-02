using System.Collections.Generic;
using System.Threading.Tasks;

namespace BatatasFritas.Infrastructure.Repositories
{
    public class InMemoryUnitOfWork : IUnitOfWork
    {
        public void BeginTransaction()
        {
            // Do nothing in memory
        }

        public Task CommitAsync()
        {
            return Task.CompletedTask;
        }

        public Task RollbackAsync()
        {
            return Task.CompletedTask;
        }

        public Task<int> ExecuteRawAsync(string sql, Dictionary<string, object>? parameters = null)
            => Task.FromResult(0);

        public void Dispose()
        {
            // Do nothing
        }
    }
}
