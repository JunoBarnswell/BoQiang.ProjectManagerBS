using Microsoft.Extensions.Caching.Memory;

namespace AsterERP.Workflow.Approval.Core.Caching;

public class CustomProcessDefinitionInfoCacheCopy<T> where T : class
{
    private const string CacheName = "cache-process-definition";
    private readonly IMemoryCache _memoryCache;

    public CustomProcessDefinitionInfoCacheCopy(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public T? Get(string id)
    {
        return _memoryCache.Get<T>($"{CacheName}:{id}");
    }

    public bool Contains(string id)
    {
        return _memoryCache.TryGetValue($"{CacheName}:{id}", out _);
    }

    public void Add(string id, T obj)
    {
        if (obj != null)
        {
            _memoryCache.Set($"{CacheName}:{id}", obj);
        }
    }

    public void Remove(string id)
    {
        _memoryCache.Remove($"{CacheName}:{id}");
    }

    public void Clear()
    {
    }
}
