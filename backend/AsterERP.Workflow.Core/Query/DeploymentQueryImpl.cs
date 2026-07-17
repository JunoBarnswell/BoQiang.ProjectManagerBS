using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public record DeploymentRecord
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Key { get; init; }
    public string? TenantId { get; init; }
    public DateTime? DeployTime { get; init; }
}

public static class DeploymentQueryProperty
{
    public const string DeploymentId = "ID_";
    public const string DeployTime = "DEPLOY_TIME_";
    public const string DeploymentName = "NAME_";
    public const string DeploymentTenantId = "TENANT_ID_";
}

public class DeploymentQueryImpl : AbstractQuery<DeploymentQueryImpl, DeploymentRecord>
{
    protected string? deploymentId;
    protected string? name;
    protected string? nameLike;
    protected string? category;
    protected string? categoryLike;
    protected string? categoryNotEquals;
    protected string? key;
    protected string? keyLike;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;
    protected string? processDefinitionKey;
    protected string? processDefinitionKeyLike;
    protected bool latest;
    protected bool latestVersion;

    private readonly IEnumerable<DeploymentRecord>? _source;

    public DeploymentQueryImpl() { }

    public DeploymentQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public DeploymentQueryImpl(IEnumerable<DeploymentRecord> source)
    {
        _source = source;
    }

    public DeploymentQueryImpl DeploymentId(string deploymentId)
    {
        this.deploymentId = deploymentId ?? throw new ArgumentNullException(nameof(deploymentId));
        return this;
    }

    public DeploymentQueryImpl DeploymentName(string deploymentName)
    {
        name = deploymentName ?? throw new ArgumentNullException(nameof(deploymentName));
        return this;
    }

    public DeploymentQueryImpl DeploymentNameLike(string nameLike)
    {
        this.nameLike = nameLike ?? throw new ArgumentNullException(nameof(nameLike));
        return this;
    }

    public DeploymentQueryImpl DeploymentCategory(string deploymentCategory)
    {
        category = deploymentCategory ?? throw new ArgumentNullException(nameof(deploymentCategory));
        return this;
    }

    public DeploymentQueryImpl DeploymentCategoryLike(string categoryLike)
    {
        this.categoryLike = categoryLike ?? throw new ArgumentNullException(nameof(categoryLike));
        return this;
    }

    public DeploymentQueryImpl DeploymentCategoryNotEquals(string categoryNotEquals)
    {
        this.categoryNotEquals = categoryNotEquals ?? throw new ArgumentNullException(nameof(categoryNotEquals));
        return this;
    }

    public DeploymentQueryImpl DeploymentKey(string deploymentKey)
    {
        key = deploymentKey ?? throw new ArgumentNullException(nameof(deploymentKey));
        return this;
    }

    public DeploymentQueryImpl DeploymentKeyLike(string deploymentKeyLike)
    {
        keyLike = deploymentKeyLike ?? throw new ArgumentNullException(nameof(deploymentKeyLike));
        return this;
    }

    public DeploymentQueryImpl DeploymentTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public DeploymentQueryImpl DeploymentTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public DeploymentQueryImpl DeploymentWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public DeploymentQueryImpl ProcessDefinitionKey(string processDefinitionKey)
    {
        this.processDefinitionKey = processDefinitionKey ?? throw new ArgumentNullException(nameof(processDefinitionKey));
        return this;
    }

    public DeploymentQueryImpl ProcessDefinitionKeyLike(string processDefinitionKeyLike)
    {
        this.processDefinitionKeyLike = processDefinitionKeyLike ?? throw new ArgumentNullException(nameof(processDefinitionKeyLike));
        return this;
    }

    public DeploymentQueryImpl Latest()
    {
        if (key == null) throw new InvalidOperationException("latest can only be used together with a deployment key");
        latest = true;
        return this;
    }

    public DeploymentQueryImpl LatestVersion()
    {
        latestVersion = true;
        return this;
    }

    public DeploymentQueryImpl OrderByDeploymentId() => OrderByProperty(DeploymentQueryProperty.DeploymentId);
    public DeploymentQueryImpl OrderByDeploymentTime() => OrderByProperty(DeploymentQueryProperty.DeployTime);
    public DeploymentQueryImpl OrderByDeploymentName() => OrderByProperty(DeploymentQueryProperty.DeploymentName);
    public DeploymentQueryImpl OrderByTenantId() => OrderByProperty(DeploymentQueryProperty.DeploymentTenantId);

    public override Task<List<DeploymentRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<DeploymentRecord?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    private IEnumerable<DeploymentRecord> ApplyFilters(IEnumerable<DeploymentRecord>? source)
    {
        if (source == null) return Enumerable.Empty<DeploymentRecord>();
        var query = source.AsEnumerable();

        if (deploymentId != null) query = query.Where(d => d.Id == deploymentId);
        if (name != null) query = query.Where(d => d.Name == name);
        if (nameLike != null) query = query.Where(d => d.Name != null && d.Name.Contains(nameLike.Replace("%", "")));
        if (category != null) query = query.Where(d => d.Category == category);
        if (categoryLike != null) query = query.Where(d => d.Category != null && d.Category.Contains(categoryLike.Replace("%", "")));
        if (categoryNotEquals != null) query = query.Where(d => d.Category != categoryNotEquals);
        if (key != null) query = query.Where(d => d.Key == key);
        if (keyLike != null) query = query.Where(d => d.Key != null && d.Key.Contains(keyLike.Replace("%", "")));
        if (tenantId != null) query = query.Where(d => d.TenantId == tenantId);
        if (tenantIdLike != null) query = query.Where(d => d.TenantId != null && d.TenantId.Contains(tenantIdLike.Replace("%", "")));
        if (withoutTenantId) query = query.Where(d => d.TenantId == null);

        if (latestVersion)
        {
            var results = query.ToList();
            query = results
                .GroupBy(d => d.Key)
                .Select(g => g.OrderByDescending(d => d.DeployTime).First())
                .AsEnumerable();
        }

        return query;
    }

    private IEnumerable<DeploymentRecord> ApplySorting(IEnumerable<DeploymentRecord> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            DeploymentQueryProperty.DeploymentId => asc ? query.OrderBy(d => d.Id) : query.OrderByDescending(d => d.Id),
            DeploymentQueryProperty.DeployTime => asc ? query.OrderBy(d => d.DeployTime) : query.OrderByDescending(d => d.DeployTime),
            DeploymentQueryProperty.DeploymentName => asc ? query.OrderBy(d => d.Name) : query.OrderByDescending(d => d.Name),
            _ => query
        };
    }
}
