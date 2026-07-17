using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Engine;

namespace AsterERP.Workflow.Core.Deploy;

public class DeploymentResource
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? DeploymentId { get; set; }
    public byte[]? Bytes { get; set; }
}

public class DeploymentManager
{
    private IDeploymentCache<ProcessDefinitionCacheEntry>? _processDefinitionCache;
    private ProcessDefinitionInfoCache? _processDefinitionInfoCache;
    private IDeploymentCache<object>? _knowledgeBaseCache;
    private List<IDeployer> _deployers = new();
    private IProcessEngineConfiguration? _processEngineConfiguration;
    private readonly List<DeploymentResource> _resources = new();
    private readonly List<DeploymentEntity> _deployments = new();

    public IDeploymentCache<ProcessDefinitionCacheEntry>? ProcessDefinitionCache
    {
        get => _processDefinitionCache;
        set => _processDefinitionCache = value;
    }

    public ProcessDefinitionInfoCache? ProcessDefinitionInfoCache
    {
        get => _processDefinitionInfoCache;
        set => _processDefinitionInfoCache = value;
    }

    public IDeploymentCache<object>? KnowledgeBaseCache
    {
        get => _knowledgeBaseCache;
        set => _knowledgeBaseCache = value;
    }

    public List<IDeployer> Deployers
    {
        get => _deployers;
        set => _deployers = value;
    }

    public IProcessEngineConfiguration? ProcessEngineConfiguration
    {
        get => _processEngineConfiguration;
        set => _processEngineConfiguration = value;
    }

    public void Deploy(DeploymentEntity deployment)
    {
        Deploy(deployment, null);
    }

    public void Deploy(DeploymentEntity deployment, Dictionary<string, object>? deploymentSettings)
    {
        foreach (var deployer in _deployers)
        {
            deployer.Deploy(deployment, deploymentSettings);
        }
    }

    public async Task DeployAsync(
        DeploymentEntity deployment,
        Dictionary<string, object>? deploymentSettings = null,
        CancellationToken cancellationToken = default)
    {
        foreach (var deployer in _deployers)
        {
            await deployer.DeployAsync(deployment, deploymentSettings, cancellationToken);
        }
    }

    public Deployer.ProcessDefinitionInfo? FindDeployedProcessDefinitionById(string processDefinitionId)
    {
        if (string.IsNullOrEmpty(processDefinitionId))
        {
            throw new AsterERP.Workflow.Common.WorkflowEngineArgumentException("Invalid process definition id : null");
        }

        if (_processDefinitionCache != null)
        {
            var cacheEntry = _processDefinitionCache.Get(processDefinitionId);
            if (cacheEntry != null)
            {
                return cacheEntry.ProcessDefinition;
            }
        }

        return ResolveProcessDefinition(processDefinitionId)?.ProcessDefinition;
    }

    public ProcessDefinitionCacheEntry? ResolveProcessDefinition(string processDefinitionId)
    {
        if (_processDefinitionCache == null) return null;

        var cachedProcessDefinition = _processDefinitionCache.Get(processDefinitionId);
        if (cachedProcessDefinition != null)
        {
            return cachedProcessDefinition;
        }

        return null;
    }

    private SqlSugar.ISqlSugarClient? GetSqlSugarClient()
    {
        return ProcessEngineServiceProviderAccessor.GetService<SqlSugar.ISqlSugarClient>(_processEngineConfiguration);
    }

    public void RemoveDeployment(string deploymentId, bool cascade)
    {
        if (_processDefinitionCache != null)
        {
            _processDefinitionCache.Clear();
        }

        var dbClient = GetSqlSugarClient();
        if (dbClient != null)
        {
            dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.DeploymentEntity>().Where(d => d.Id == deploymentId).ExecuteCommand();
            dbClient.Deleteable<AsterERP.Workflow.Persistence.Entities.ResourceEntity>().Where(r => r.DeploymentId == deploymentId).ExecuteCommand();
        }

        _resources.RemoveAll(r => r.DeploymentId == deploymentId);
        _deployments.RemoveAll(d => d.Id == deploymentId);
    }

    public void AddResource(DeploymentResource resource)
    {
        _resources.Add(resource);
        var dbClient = GetSqlSugarClient();
        if (dbClient != null && !string.IsNullOrEmpty(resource.Id))
        {
            dbClient.Storageable(new AsterERP.Workflow.Persistence.Entities.ResourceEntity
            {
                Id = resource.Id,
                DeploymentId = resource.DeploymentId,
                Name = resource.Name,
                Bytes = resource.Bytes
            }).ExecuteCommand();
        }
    }

    public void AddDeployment(DeploymentEntity deployment)
    {
        _deployments.Add(deployment);
        var dbClient = GetSqlSugarClient();
        if (dbClient != null && !string.IsNullOrEmpty(deployment.Id))
        {
            dbClient.Storageable(new AsterERP.Workflow.Persistence.Entities.DeploymentEntity
            {
                Id = deployment.Id,
                Name = deployment.Name,
                Category = deployment.Category,
                Key = deployment.Key,
                TenantId = deployment.TenantId,
                DeployTime = deployment.DeployTime
            }).ExecuteCommand();
        }
    }

    public DeploymentResource? FindResourceByDeploymentIdAndResourceName(string deploymentId, string resourceName)
    {
        var dbClient = GetSqlSugarClient();
        if (dbClient != null)
        {
            var dbResource = dbClient.Queryable<AsterERP.Workflow.Persistence.Entities.ResourceEntity>()
                .First(r => r.DeploymentId == deploymentId && r.Name == resourceName);
            if (dbResource != null)
            {
                return new DeploymentResource
                {
                    Id = dbResource.Id,
                    Name = dbResource.Name,
                    DeploymentId = dbResource.DeploymentId,
                    Bytes = dbResource.Bytes
                };
            }
        }
        return _resources.FirstOrDefault(r => r.DeploymentId == deploymentId && r.Name == resourceName);
    }

    public List<DeploymentResource> FindResourcesByDeploymentId(string deploymentId)
    {
        var dbClient = GetSqlSugarClient();
        if (dbClient != null)
        {
            var dbResources = dbClient.Queryable<AsterERP.Workflow.Persistence.Entities.ResourceEntity>()
                .Where(r => r.DeploymentId == deploymentId).ToList();
            if (dbResources != null && dbResources.Count > 0)
            {
                return dbResources.Select(dbResource => new DeploymentResource
                {
                    Id = dbResource.Id,
                    Name = dbResource.Name,
                    DeploymentId = dbResource.DeploymentId,
                    Bytes = dbResource.Bytes
                }).ToList();
            }
        }
        return _resources.Where(r => r.DeploymentId == deploymentId).ToList();
    }

    public DeploymentEntity? FindDeploymentById(string deploymentId)
    {
        var dbClient = GetSqlSugarClient();
        if (dbClient != null)
        {
            var dbDeployment = dbClient.Queryable<AsterERP.Workflow.Persistence.Entities.DeploymentEntity>()
                .First(d => d.Id == deploymentId);
            if (dbDeployment != null)
            {
                return new DeploymentEntity
                {
                    Id = dbDeployment.Id,
                    Name = dbDeployment.Name,
                    Category = dbDeployment.Category,
                    Key = dbDeployment.Key,
                    TenantId = dbDeployment.TenantId,
                    DeployTime = dbDeployment.DeployTime ?? AbpTimeIdProvider.UtcNow,
                    IsNew = false
                };
            }
        }
        return _deployments.FirstOrDefault(d => d.Id == deploymentId);
    }

    public IReadOnlyCollection<ProcessDefinitionCacheEntry> GetProcessDefinitionCacheEntries()
    {
        if (_processDefinitionCache == null) return Array.Empty<ProcessDefinitionCacheEntry>();
        return _processDefinitionCache.GetAll().ToList();
    }
}

