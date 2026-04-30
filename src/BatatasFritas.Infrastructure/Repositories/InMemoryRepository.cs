using BatatasFritas.Domain.Entities;
using BatatasFritas.Shared.DTOs;
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

        public Task<IEnumerable<T>> FindManyAsync(Expression<Func<T, bool>> predicate) =>
            Task.FromResult(_storage.Values.AsQueryable().Where(predicate).AsEnumerable());

        public Task<PagedResult<T>> GetPagedAsync(int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            var all   = _storage.Values.ToList();
            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<T> { Items = items, TotalCount = all.Count, Page = page, PageSize = pageSize });
        }

        public Task<PagedResult<T>> GetPagedAsync(Expression<Func<T, bool>> predicate, int page, int pageSize)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            var all   = _storage.Values.AsQueryable().Where(predicate).ToList();
            var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult(new PagedResult<T> { Items = items, TotalCount = all.Count, Page = page, PageSize = pageSize });
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
