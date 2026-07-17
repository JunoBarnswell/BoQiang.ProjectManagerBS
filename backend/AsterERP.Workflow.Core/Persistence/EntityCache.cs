using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace AsterERP.Workflow.Core.Persistence;

public interface IEntity
{
    string Id { get; }
    object? GetPersistentState();
}

public interface IEntityCache
{
    T? FindEntity<T>(string id) where T : class;
    void AddEntity(IEntity entity, bool storeState = true);
    void RemoveEntity(Type entityClass, string entityId);
    List<T> GetEntities<T>() where T : class;
    IReadOnlyCollection<CachedEntity> GetCachedEntities<T>() where T : class;
    IDictionary<Type, IDictionary<string, CachedEntity>> GetAllCachedEntities();
}

public class CachedEntity
{
    public IEntity Entity { get; }
    public object? OriginalPersistentState { get; set; }
    public bool IsUpdated { get; set; }
    public bool IsDeleted { get; set; }

    public CachedEntity(IEntity entity, bool storeState)
    {
        Entity = entity;
        if (storeState)
        {
            OriginalPersistentState = entity.GetPersistentState();
        }
    }

    public bool HasChanged()
    {
        var currentState = Entity.GetPersistentState();
        return currentState != null && !currentState.Equals(OriginalPersistentState);
    }
}

public class EntityCacheImpl : IEntityCache
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<string, CachedEntity>> _cachedObjects = new();

    public void AddEntity(IEntity entity, bool storeState = true)
    {
        var classCache = _cachedObjects.GetOrAdd(entity.GetType(), _ => new ConcurrentDictionary<string, CachedEntity>());
        var cachedObject = new CachedEntity(entity, storeState);
        classCache[entity.Id] = cachedObject;
    }

    public T? FindEntity<T>(string id) where T : class
    {
        var classCache = FindClassCache(typeof(T));
        if (classCache != null && classCache.TryGetValue(id, out var cachedObject))
        {
            return cachedObject.Entity as T;
        }
        return null;
    }

    public void RemoveEntity(Type entityClass, string entityId)
    {
        if (_cachedObjects.TryGetValue(entityClass, out var classCache))
        {
            classCache.TryRemove(entityId, out _);
        }
    }

    public List<T> GetEntities<T>() where T : class
    {
        var classCache = FindClassCache(typeof(T));
        if (classCache != null)
        {
            return classCache.Values
                .Where(c => !c.IsDeleted)
                .Select(c => c.Entity as T)
                .Where(e => e != null)
                .Cast<T>()
                .ToList();
        }
        return new List<T>();
    }

    public IReadOnlyCollection<CachedEntity> GetCachedEntities<T>() where T : class
    {
        var classCache = FindClassCache(typeof(T));
        if (classCache != null)
        {
            return classCache.Values.ToList();
        }
        return Array.Empty<CachedEntity>();
    }

    public IDictionary<Type, IDictionary<string, CachedEntity>> GetAllCachedEntities()
    {
        var result = new Dictionary<Type, IDictionary<string, CachedEntity>>();
        foreach (var kvp in _cachedObjects)
        {
            result[kvp.Key] = new Dictionary<string, CachedEntity>(kvp.Value);
        }
        return result;
    }

    private ConcurrentDictionary<string, CachedEntity>? FindClassCache(Type entityClass)
    {
        if (_cachedObjects.TryGetValue(entityClass, out var classCache))
        {
            return classCache;
        }

        foreach (var kvp in _cachedObjects)
        {
            if (entityClass.IsAssignableFrom(kvp.Key))
            {
                return kvp.Value;
            }
        }

        return null;
    }
}
