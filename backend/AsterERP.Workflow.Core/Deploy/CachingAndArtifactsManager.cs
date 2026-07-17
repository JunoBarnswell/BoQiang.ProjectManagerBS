namespace AsterERP.Workflow.Core.Deploy;

public class CachingAndArtifactsManager
{
    private readonly IDeploymentCache<ProcessDefinitionCacheEntry> _processDefinitionCache;
    private readonly ProcessDefinitionInfoCache _processDefinitionInfoCache;

    public CachingAndArtifactsManager(
        IDeploymentCache<ProcessDefinitionCacheEntry> processDefinitionCache,
        ProcessDefinitionInfoCache processDefinitionInfoCache)
    {
        _processDefinitionCache = processDefinitionCache;
        _processDefinitionInfoCache = processDefinitionInfoCache;
    }

    public void AddProcessDefinitionCacheEntry(string processDefinitionId, ProcessDefinitionCacheEntry entry)
    {
        _processDefinitionCache.Add(processDefinitionId, entry);
    }

    public ProcessDefinitionCacheEntry? GetProcessDefinitionCacheEntry(string processDefinitionId)
    {
        return _processDefinitionCache.Get(processDefinitionId);
    }

    public void RemoveProcessDefinitionCacheEntry(string processDefinitionId)
    {
        _processDefinitionCache.Remove(processDefinitionId);
    }

    public ProcessDefinitionInfoCacheObject GetProcessDefinitionInfo(string processDefinitionId)
    {
        return _processDefinitionInfoCache.Get(processDefinitionId);
    }

    public void UpdateProcessDefinitionInfo(string processDefinitionId, ProcessDefinitionInfoCacheObject infoObject)
    {
        _processDefinitionInfoCache.Add(processDefinitionId, infoObject);
    }

    public void ClearAll()
    {
        _processDefinitionCache.Clear();
        _processDefinitionInfoCache.Clear();
    }
}
