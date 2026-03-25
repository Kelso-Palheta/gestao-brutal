using BatatasFritas.Domain.Entities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

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

        public Task<IEnumerable<T>> GetAllAsync() =>
            Task.FromResult(_storage.Values.AsEnumerable());

        public Task<T?> FindAsync(Expression<Func<T, bool>> predicate)
        {
            var result = _storage.Values.AsQueryable().FirstOrDefault(predicate);
            return Task.FromResult(result);
        }

        public Task AddAsync(T entity)
        {
            if (entity.Id == 0)
            {
                var idProp = typeof(EntityBase).GetProperty("Id");
                idProp?.DeclaringType?.GetProperty("Id")
                       ?.GetSetMethod(true)
                       ?.Invoke(entity, new object[] { Interlocked.Increment(ref _idCounter) });
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
