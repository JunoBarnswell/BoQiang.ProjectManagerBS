using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Deployer;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;

namespace AsterERP.Workflow.Core.Cmd;

public class DeploymentResult
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? TenantId { get; init; }
    public int Version { get; init; }
    public DateTime DeploymentTime { get; init; }
    public List<string> ResourceNames { get; init; } = new();
}

public class DeployCmd : ICommand<DeploymentResult>
{
    private readonly string? _name;
    private readonly string? _category;
    private readonly string? _tenantId;
    private readonly Dictionary<string, byte[]> _resources;
    private readonly bool _enableDuplicateFiltering;

    public DeployCmd(
        string? name,
        string? category,
        string? tenantId,
        Dictionary<string, byte[]> resources,
        bool enableDuplicateFiltering = false)
    {
        _name = name;
        _category = category;
        _tenantId = tenantId;
        _resources = resources ?? new Dictionary<string, byte[]>();
        _enableDuplicateFiltering = enableDuplicateFiltering;
    }


    public async Task<DeploymentResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var deploymentId = $"deployment-{AbpTimeIdProvider.NewGuid("N")}";
        var deployer = ProcessEngineServiceProviderAccessor.GetService<BpmnDeployer>(context.ProcessEngineConfiguration);
        var db = ProcessEngineServiceProviderAccessor.GetService<ISqlSugarClient>(context.ProcessEngineConfiguration);

        ParsedDeployment? parsedDeployment = null;
        if (deployer != null && _resources.Count > 0)
        {
            var resourceNames = new List<string>(_resources.Keys);
            var resourceBytes = new List<byte[]>(_resources.Values);
            parsedDeployment = await deployer.DeployAsync(deploymentId, resourceNames, resourceBytes.ToArray(), cancellationToken);
        }

        if (_enableDuplicateFiltering &&
            parsedDeployment != null &&
            db != null)
        {
            var duplicateDeployment = await TryResolveDuplicateDeploymentAsync(
                parsedDeployment,
                db,
                cancellationToken);

            if (duplicateDeployment != null)
            {
                return duplicateDeployment;
            }
        }

        if (parsedDeployment != null && db != null)
        {
            foreach (var pd in parsedDeployment.ProcessDefinitions)
            {
                var latestDefinition = await FindLatestProcessDefinitionAsync(
                    db,
                    pd.ProcessDefinition.Key,
                    _tenantId,
                    cancellationToken);
                var nextVersion = (latestDefinition?.Version ?? 0) + 1;

                var oldId = pd.ProcessDefinition.Id;
                var newId = BuildProcessDefinitionId(pd.ProcessDefinition.Key, nextVersion, _tenantId);

                pd.ProcessDefinition.Version = nextVersion;
                parsedDeployment.UpdateProcessDefinitionId(oldId, newId);
            }
        }

        var deployment = new DeploymentResult
        {
            Id = deploymentId,
            Name = _name,
            Category = _category,
            TenantId = _tenantId,
            Version = parsedDeployment == null || parsedDeployment.ProcessDefinitions.Count == 0
                ? 1
                : parsedDeployment.ProcessDefinitions.Max(pd => pd.ProcessDefinition.Version),
            DeploymentTime = AbpTimeIdProvider.UtcNow,
            ResourceNames = new List<string>(_resources.Keys)
        };

        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
        if (store?.IsEnabled == true)
        {
            await store.PersistDeploymentAsync(deployment, _resources, cancellationToken);
            if (parsedDeployment != null)
            {
                foreach (var pd in parsedDeployment.ProcessDefinitions)
                {
                    pd.ProcessDefinition.TenantId = _tenantId;
                    pd.ProcessDefinition.Category = _category;
                    pd.ProcessDefinition.DeploymentId = deploymentId;
                    var process = parsedDeployment.GetProcessForProcessDefinition(pd.ProcessDefinition)
                        ?? pd.BpmnModel.Processes.FirstOrDefault()
                        ?? throw new InvalidOperationException($"Unable to resolve BPMN process for definition '{pd.ProcessDefinition.Id}'.");

                    await store.PersistProcessDefinitionAsync(
                        pd.ProcessDefinition,
                        pd.BpmnModel,
                        process,
                        cancellationToken);
                }
            }
        }

        if (parsedDeployment != null)
        {
            var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
            if (deploymentManager?.ProcessDefinitionCache != null)
            {
                foreach (var pd in parsedDeployment.ProcessDefinitions)
                {
                    var cacheEntry = new Deploy.ProcessDefinitionCacheEntry(pd.ProcessDefinition, pd.BpmnModel, pd.BpmnModel.Processes.FirstOrDefault());
                    deploymentManager.ProcessDefinitionCache.Add(pd.ProcessDefinition.Id, cacheEntry);
                }
            }
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateEntityEvent(
                    WorkflowEventType.ENTITY_CREATED, deployment));
        }

        return deployment;
    }

    private async Task<DeploymentResult?> TryResolveDuplicateDeploymentAsync(
        ParsedDeployment parsedDeployment,
        ISqlSugarClient db,
        CancellationToken cancellationToken)
    {
        var definitionKeys = parsedDeployment.ProcessDefinitions
            .Select(item => item.ProcessDefinition.Key)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (definitionKeys.Count == 0)
        {
            return null;
        }

        var allDefinitions = await db.Queryable<ProcessDefinitionEntity>()
            .Where(item => definitionKeys.Contains(item.Key!))
            .WhereIF(!string.IsNullOrWhiteSpace(_tenantId), item => item.TenantId == _tenantId)
            .OrderBy(item => item.Version, OrderByType.Desc)
            .ToListAsync(cancellationToken);

        var latestDefinitions = allDefinitions
            .GroupBy(item => item.Key, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Version).First())
            .ToList();

        if (latestDefinitions.Count != definitionKeys.Count || latestDefinitions.Any(item => string.IsNullOrWhiteSpace(item.DeploymentId)))
        {
            return null;
        }

        var deploymentIds = latestDefinitions
            .Select(item => item.DeploymentId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (deploymentIds.Count != 1)
        {
            return null;
        }

        var existingDeploymentId = deploymentIds[0];
        var versions = latestDefinitions.Select(item => item.Version).ToList();

        var existingResources = await db.Queryable<ResourceEntity>()
            .Where(resource => resource.DeploymentId == existingDeploymentId)
            .ToListAsync(cancellationToken);
        if (!ResourcesMatch(existingResources, _resources))
        {
            return null;
        }

        var existingDeployment = await db.Queryable<DeploymentEntity>()
            .InSingleAsync(existingDeploymentId);
        if (existingDeployment == null)
        {
            return null;
        }

        return new DeploymentResult
        {
            Id = existingDeployment.Id,
            Name = existingDeployment.Name,
            Category = existingDeployment.Category,
            TenantId = existingDeployment.TenantId,
            Version = versions.Count == 0 ? 1 : versions.Max(),
            DeploymentTime = existingDeployment.DeployTime ?? AbpTimeIdProvider.UtcNow,
            ResourceNames = existingResources
                .Where(resource => !string.IsNullOrWhiteSpace(resource.Name))
                .Select(resource => resource.Name!)
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }

    private static async Task<ProcessDefinitionEntity?> FindLatestProcessDefinitionAsync(
        ISqlSugarClient db,
        string processDefinitionKey,
        string? tenantId,
        CancellationToken cancellationToken)
    {
        return await db.Queryable<ProcessDefinitionEntity>()
            .Where(item => item.Key == processDefinitionKey)
            .WhereIF(!string.IsNullOrWhiteSpace(tenantId), item => item.TenantId == tenantId)
            .OrderBy(item => item.Version, OrderByType.Desc)
            .FirstAsync(cancellationToken);
    }

    private static string BuildProcessDefinitionId(string key, int version, string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return $"{key}:{version}";
        }

        return $"{NormalizeDefinitionIdSegment(tenantId)}:{key}:{version}";
    }

    private static string NormalizeDefinitionIdSegment(string value)
    {
        var normalized = new string(value.Trim()
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(normalized) ? "APP" : normalized.ToUpperInvariant();
    }

    private static bool ResourcesMatch(
        IReadOnlyCollection<ResourceEntity> existingResources,
        IReadOnlyDictionary<string, byte[]> requestedResources)
    {
        if (existingResources.Count != requestedResources.Count)
        {
            return false;
        }

        foreach (var existingResource in existingResources)
        {
            if (string.IsNullOrWhiteSpace(existingResource.Name) ||
                !requestedResources.TryGetValue(existingResource.Name, out var requestedBytes))
            {
                return false;
            }

            var existingHash = ComputeSha256(existingResource.Bytes ?? Array.Empty<byte>());
            var requestedHash = ComputeSha256(requestedBytes);
            if (!existingHash.SequenceEqual(requestedHash))
            {
                return false;
            }
        }

        return true;
    }

    private static byte[] ComputeSha256(byte[] bytes)
    {
        return SHA256.HashData(bytes);
    }
}

