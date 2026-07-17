using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AsterERP.Workflow.Core.Deploy;

public class DefaultDeploymentCache<T> : IDeploymentCache<T>
{
    private readonly ConcurrentDictionary<string, T> _cache;
    private readonly int _limit;

    public DefaultDeploymentCache()
    {
        _cache = new ConcurrentDictionary<string, T>();
        _limit = -1;
    }

    public DefaultDeploymentCache(int limit)
    {
        _cache = new ConcurrentDictionary<string, T>();
        _limit = limit;
    }

    public T? Get(string id)
    {
        _cache.TryGetValue(id, out var value);
        return value;
    }

    public bool Contains(string id)
    {
        return _cache.ContainsKey(id);
    }

    public void Add(string id, T obj)
    {
        _cache[id] = obj;

        if (_limit > 0 && _cache.Count > _limit)
        {
            var oldestKey = _cache.Keys.FirstOrDefault();
            if (oldestKey != null)
            {
                _cache.TryRemove(oldestKey, out _);
            }
        }
    }

    public void Remove(string id)
    {
        _cache.TryRemove(id, out _);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public IEnumerable<T> GetAll()
    {
        return _cache.Values.ToList();
    }

    public int Size => _cache.Count;
}
