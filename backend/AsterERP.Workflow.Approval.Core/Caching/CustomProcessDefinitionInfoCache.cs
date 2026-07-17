using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace AsterERP.Workflow.Approval.Core.Caching;

public class CustomProcessDefinitionInfoCache<T> where T : class
{
    private readonly ConcurrentDictionary<string, T> _cache = new();

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
    }

    public void Remove(string id)
    {
        _cache.TryRemove(id, out _);
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public ICollection<T> GetAll()
    {
        return _cache.Values;
    }

    public int Size()
    {
        return _cache.Count;
    }
}
