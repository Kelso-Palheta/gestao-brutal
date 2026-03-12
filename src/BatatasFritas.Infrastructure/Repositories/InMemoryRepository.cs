using BatatasFritas.Domain.Entities;
using System.Collections.Concurrent;

namespace BatatasFritas.Infrastructure.Repositories
{
    public class InMemoryRepository<T> : IRepository<T> where T : EntityBase
    {
        private static readonly ConcurrentDictionary<int, T> _storage = new();
        private static int _idCounter = 1;

        public Task<T?> GetByIdAsync(int id)
        {
            _storage.TryGetValue(id, out var entity);
            return Task.FromResult(entity);
        }

        public Task<IEnumerable<T>> GetAllAsync()
        {
            return Task.FromResult(_storage.Values.AsEnumerable());
        }

        public Task AddAsync(T entity)
        {
            var prop = typeof(EntityBase).GetProperty("Id");
            if (prop != null && entity.Id == 0)
            {
                prop.DeclaringType?.GetProperty("Id")?.GetSetMethod(true)?.Invoke(entity, new object[] { Interlocked.Increment(ref _idCounter) });
            }
            _storage.TryAdd(entity.Id, entity);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(T entity)
        {
            _storage[entity.Id] = entity;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(T entity)
        {
            _storage.TryRemove(entity.Id, out _);
            return Task.CompletedTask;
        }
    }
}
