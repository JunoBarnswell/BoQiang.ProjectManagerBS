using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public static class ProcessDefinitionQueryProperty
{
    public const string ProcessDefinitionId = "ID_";
    public const string ProcessDefinitionKey = "KEY_";
    public const string ProcessDefinitionCategory = "CATEGORY_";
    public const string ProcessDefinitionName = "NAME_";
    public const string ProcessDefinitionVersion = "VERSION_";
    public const string ProcessDefinitionAppVersion = "APP_VERSION_";
    public const string DeploymentId = "DEPLOYMENT_ID_";
    public const string TenantId = "TENANT_ID_";
}

public class ProcessDefinitionQueryImpl : AbstractQuery<ProcessDefinitionQueryImpl, ProcessDefinitionRecord>
{
    protected string? id;
    protected HashSet<string>? ids;
    protected string? category;
    protected string? categoryLike;
    protected string? categoryNotEquals;
    protected string? name;
    protected string? nameLike;
    protected string? deploymentId;
    protected HashSet<string>? deploymentIds;
    protected string? key;
    protected string? idOrKey;
    protected string? keyLike;
    protected HashSet<string>? keys;
    protected string? resourceName;
    protected string? resourceNameLike;
    protected int? version;
    protected int? versionGt;
    protected int? versionGte;
    protected int? versionLt;
    protected int? versionLte;
    protected bool latest;
    protected bool isSuspended;
    protected bool isActive;
    protected string? authorizationUserId;
    protected List<string>? authorizationGroups;
    protected string? procDefId;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected string? eventSubscriptionName;
    protected string? eventSubscriptionType;

    private readonly IEnumerable<ProcessDefinitionRecord>? _source;

    public ProcessDefinitionQueryImpl() { }

    public ProcessDefinitionQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public ProcessDefinitionQueryImpl(IEnumerable<ProcessDefinitionRecord> source)
    {
        _source = source;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionId(string processDefinitionId)
    {
        id = processDefinitionId;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionIds(HashSet<string> processDefinitionIds)
    {
        ids = processDefinitionIds;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionCategory(string category)
    {
        this.category = category ?? throw new ArgumentNullException(nameof(category));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionCategoryLike(string categoryLike)
    {
        this.categoryLike = categoryLike ?? throw new ArgumentNullException(nameof(categoryLike));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionCategoryNotEquals(string categoryNotEquals)
    {
        this.categoryNotEquals = categoryNotEquals ?? throw new ArgumentNullException(nameof(categoryNotEquals));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionName(string name)
    {
        this.name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionNameLike(string nameLike)
    {
        this.nameLike = nameLike ?? throw new ArgumentNullException(nameof(nameLike));
        return this;
    }

    public ProcessDefinitionQueryImpl DeploymentId(string deploymentId)
    {
        this.deploymentId = deploymentId ?? throw new ArgumentNullException(nameof(deploymentId));
        return this;
    }

    public ProcessDefinitionQueryImpl DeploymentIds(HashSet<string> deploymentIds)
    {
        this.deploymentIds = deploymentIds ?? throw new ArgumentNullException(nameof(deploymentIds));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionKey(string key)
    {
        this.key = key ?? throw new ArgumentNullException(nameof(key));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionIdOrKey(string idOrKey)
    {
        this.idOrKey = idOrKey ?? throw new ArgumentNullException(nameof(idOrKey));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionKeys(HashSet<string> keys)
    {
        this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionKeyLike(string keyLike)
    {
        this.keyLike = keyLike ?? throw new ArgumentNullException(nameof(keyLike));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionResourceName(string resourceName)
    {
        this.resourceName = resourceName ?? throw new ArgumentNullException(nameof(resourceName));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionResourceNameLike(string resourceNameLike)
    {
        this.resourceNameLike = resourceNameLike ?? throw new ArgumentNullException(nameof(resourceNameLike));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionVersion(int? version)
    {
        CheckVersion(version);
        this.version = version;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionVersionGreaterThan(int? processDefinitionVersion)
    {
        CheckVersion(processDefinitionVersion);
        versionGt = processDefinitionVersion;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionVersionGreaterThanOrEquals(int? processDefinitionVersion)
    {
        CheckVersion(processDefinitionVersion);
        versionGte = processDefinitionVersion;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionVersionLowerThan(int? processDefinitionVersion)
    {
        CheckVersion(processDefinitionVersion);
        versionLt = processDefinitionVersion;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionVersionLowerThanOrEquals(int? processDefinitionVersion)
    {
        CheckVersion(processDefinitionVersion);
        versionLte = processDefinitionVersion;
        return this;
    }

    private void CheckVersion(int? v)
    {
        if (v == null) throw new ArgumentNullException(nameof(v), "Version is null");
        if (v <= 0) throw new ArgumentException("Version must be positive");
    }

    public ProcessDefinitionQueryImpl LatestVersion()
    {
        latest = true;
        return this;
    }

    public ProcessDefinitionQueryImpl Active()
    {
        isActive = true;
        isSuspended = false;
        return this;
    }

    public ProcessDefinitionQueryImpl Suspended()
    {
        isSuspended = true;
        isActive = false;
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public ProcessDefinitionQueryImpl ProcessDefinitionWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public ProcessDefinitionQueryImpl StartableByUser(string userId)
    {
        authorizationUserId = userId ?? throw new ArgumentNullException(nameof(userId));
        return this;
    }

    public ProcessDefinitionQueryImpl StartableByGroups(List<string> groupIds)
    {
        authorizationGroups = groupIds;
        return this;
    }

    public ProcessDefinitionQueryImpl MessageEventSubscription(string messageName)
    {
        return EventSubscription("message", messageName);
    }

    public ProcessDefinitionQueryImpl EventSubscription(string eventType, string eventName)
    {
        eventSubscriptionType = eventType ?? throw new ArgumentNullException(nameof(eventType));
        eventSubscriptionName = eventName ?? throw new ArgumentNullException(nameof(eventName));
        return this;
    }

    public ProcessDefinitionQueryImpl OrderByDeploymentId() => OrderByProperty(ProcessDefinitionQueryProperty.DeploymentId);
    public ProcessDefinitionQueryImpl OrderByProcessDefinitionKey() => OrderByProperty(ProcessDefinitionQueryProperty.ProcessDefinitionKey);
    public ProcessDefinitionQueryImpl OrderByProcessDefinitionCategory() => OrderByProperty(ProcessDefinitionQueryProperty.ProcessDefinitionCategory);
    public ProcessDefinitionQueryImpl OrderByProcessDefinitionId() => OrderByProperty(ProcessDefinitionQueryProperty.ProcessDefinitionId);
    public ProcessDefinitionQueryImpl OrderByProcessDefinitionVersion() => OrderByProperty(ProcessDefinitionQueryProperty.ProcessDefinitionVersion);
    public ProcessDefinitionQueryImpl OrderByProcessDefinitionAppVersion() => OrderByProperty(ProcessDefinitionQueryProperty.ProcessDefinitionAppVersion);
    public ProcessDefinitionQueryImpl OrderByProcessDefinitionName() => OrderByProperty(ProcessDefinitionQueryProperty.ProcessDefinitionName);
    public ProcessDefinitionQueryImpl OrderByTenantId() => OrderByProperty(ProcessDefinitionQueryProperty.TenantId);

    public override Task<List<ProcessDefinitionRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<ProcessDefinitionRecord?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    private IEnumerable<ProcessDefinitionRecord> ApplyFilters(IEnumerable<ProcessDefinitionRecord>? source)
    {
        if (source == null) return Enumerable.Empty<ProcessDefinitionRecord>();
        var query = source.AsEnumerable();

        if (id != null) query = query.Where(pd => pd.Id == id);
        if (ids != null) query = query.Where(pd => ids.Contains(pd.Id));
        if (category != null) query = query.Where(pd => pd.Category == category);
        if (categoryLike != null) query = query.Where(pd => pd.Category != null && pd.Category.Contains(categoryLike.Replace("%", "")));
        if (categoryNotEquals != null) query = query.Where(pd => pd.Category != categoryNotEquals);
        if (name != null) query = query.Where(pd => pd.Name == name);
        if (nameLike != null) query = query.Where(pd => pd.Name != null && pd.Name.Contains(nameLike.Replace("%", "")));
        if (deploymentId != null) query = query.Where(pd => pd.DeploymentId == deploymentId);
        if (deploymentIds != null) query = query.Where(pd => pd.DeploymentId != null && deploymentIds.Contains(pd.DeploymentId));
        if (key != null) query = query.Where(pd => pd.Key == key);
        if (idOrKey != null) query = query.Where(pd => pd.Id == idOrKey || pd.Key == idOrKey);
        if (keyLike != null) query = query.Where(pd => pd.Key != null && pd.Key.Contains(keyLike.Replace("%", "")));
        if (keys != null) query = query.Where(pd => pd.Key != null && keys.Contains(pd.Key));
        if (version.HasValue) query = query.Where(pd => pd.Version == version.Value);
        if (versionGt.HasValue) query = query.Where(pd => pd.Version > versionGt.Value);
        if (versionGte.HasValue) query = query.Where(pd => pd.Version >= versionGte.Value);
        if (versionLt.HasValue) query = query.Where(pd => pd.Version < versionLt.Value);
        if (versionLte.HasValue) query = query.Where(pd => pd.Version <= versionLte.Value);
        if (isActive) query = query.Where(pd => !pd.IsSuspended);
        if (isSuspended) query = query.Where(pd => pd.IsSuspended);
        if (tenantId != null) query = query.Where(pd => pd.TenantId == tenantId);
        if (tenantIdLike != null) query = query.Where(pd => pd.TenantId != null && pd.TenantId.Contains(tenantIdLike.Replace("%", "")));
        if (withoutTenantId) query = query.Where(pd => pd.TenantId == null);

        if (latest)
        {
            var results = query.ToList();
            query = results
                .GroupBy(pd => pd.Key)
                .Select(g => g.OrderByDescending(pd => pd.Version).First())
                .AsEnumerable();
        }

        return query;
    }

    private IEnumerable<ProcessDefinitionRecord> ApplySorting(IEnumerable<ProcessDefinitionRecord> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            ProcessDefinitionQueryProperty.ProcessDefinitionId => asc ? query.OrderBy(pd => pd.Id) : query.OrderByDescending(pd => pd.Id),
            ProcessDefinitionQueryProperty.ProcessDefinitionKey => asc ? query.OrderBy(pd => pd.Key) : query.OrderByDescending(pd => pd.Key),
            ProcessDefinitionQueryProperty.ProcessDefinitionCategory => asc ? query.OrderBy(pd => pd.Category) : query.OrderByDescending(pd => pd.Category),
            ProcessDefinitionQueryProperty.ProcessDefinitionName => asc ? query.OrderBy(pd => pd.Name) : query.OrderByDescending(pd => pd.Name),
            ProcessDefinitionQueryProperty.ProcessDefinitionVersion => asc ? query.OrderBy(pd => pd.Version) : query.OrderByDescending(pd => pd.Version),
            ProcessDefinitionQueryProperty.DeploymentId => asc ? query.OrderBy(pd => pd.DeploymentId) : query.OrderByDescending(pd => pd.DeploymentId),
            _ => query
        };
    }
}
