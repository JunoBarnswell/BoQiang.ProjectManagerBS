using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using AsterERP.Workflow.Core.Command;

namespace AsterERP.Workflow.Core.Deploy;

public class ProcessDefinitionInfoCache
{
    private readonly ConcurrentDictionary<string, ProcessDefinitionInfoCacheObject> _cache;
    private readonly ICommandExecutor? _commandExecutor;

    public ProcessDefinitionInfoCache(ICommandExecutor? commandExecutor)
    {
        _commandExecutor = commandExecutor;
        _cache = new ConcurrentDictionary<string, ProcessDefinitionInfoCacheObject>();
    }

    public ProcessDefinitionInfoCache(ICommandExecutor? commandExecutor, int limit)
    {
        _commandExecutor = commandExecutor;
        _cache = new ConcurrentDictionary<string, ProcessDefinitionInfoCacheObject>();
    }

    public ProcessDefinitionInfoCacheObject Get(string processDefinitionId)
    {
        if (_cache.TryGetValue(processDefinitionId, out var cacheObject))
        {
            return cacheObject;
        }

        cacheObject = new ProcessDefinitionInfoCacheObject
        {
            Revision = 0,
            InfoNode = new JsonObject()
        };

        _cache[processDefinitionId] = cacheObject;
        return cacheObject;
    }

    public void Add(string id, ProcessDefinitionInfoCacheObject obj)
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

    public int Size => _cache.Count;
}
