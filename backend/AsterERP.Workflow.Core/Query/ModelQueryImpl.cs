using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Query;

public record ModelRecord
{
    public string Id { get; init; } = null!;
    public string? Name { get; init; }
    public string? Category { get; init; }
    public string? Key { get; init; }
    public int? Version { get; init; }
    public string? DeploymentId { get; init; }
    public string? TenantId { get; init; }
    public DateTime? CreateTime { get; init; }
    public DateTime? LastUpdateTime { get; init; }
    public string? MetaInfo { get; init; }
    public byte[]? EditorSource { get; init; }
    public byte[]? EditorSourceExtra { get; init; }
}

public static class ModelQueryProperty
{
    public const string ModelId = "ID_";
    public const string ModelCategory = "CATEGORY_";
    public const string ModelKey = "KEY_";
    public const string ModelVersion = "VERSION_";
    public const string ModelName = "NAME_";
    public const string ModelCreateTime = "CREATE_TIME_";
    public const string ModelLastUpdateTime = "LAST_UPDATE_TIME_";
    public const string ModelTenantId = "TENANT_ID_";
}

public class ModelQueryImpl : AbstractQuery<ModelQueryImpl, ModelRecord>
{
    protected string? id;
    protected string? category;
    protected string? categoryLike;
    protected string? categoryNotEquals;
    protected string? name;
    protected string? nameLike;
    protected string? key;
    protected int? version;
    protected bool latest;
    protected string? deploymentId;
    protected bool notDeployed;
    protected bool deployed;
    protected string? tenantId;
    protected string? tenantIdLike;
    protected bool withoutTenantId;

    private readonly IEnumerable<ModelRecord>? _source;

    public ModelQueryImpl() { }

    public ModelQueryImpl(ICommandExecutor commandExecutor) : base(commandExecutor) { }

    public ModelQueryImpl(IEnumerable<ModelRecord> source)
    {
        _source = source;
    }

    public ModelQueryImpl ModelId(string modelId)
    {
        id = modelId;
        return this;
    }

    public ModelQueryImpl ModelCategory(string category)
    {
        this.category = category ?? throw new ArgumentNullException(nameof(category));
        return this;
    }

    public ModelQueryImpl ModelCategoryLike(string categoryLike)
    {
        this.categoryLike = categoryLike ?? throw new ArgumentNullException(nameof(categoryLike));
        return this;
    }

    public ModelQueryImpl ModelCategoryNotEquals(string categoryNotEquals)
    {
        this.categoryNotEquals = categoryNotEquals ?? throw new ArgumentNullException(nameof(categoryNotEquals));
        return this;
    }

    public ModelQueryImpl ModelName(string name)
    {
        this.name = name ?? throw new ArgumentNullException(nameof(name));
        return this;
    }

    public ModelQueryImpl ModelNameLike(string nameLike)
    {
        this.nameLike = nameLike ?? throw new ArgumentNullException(nameof(nameLike));
        return this;
    }

    public ModelQueryImpl ModelKey(string key)
    {
        this.key = key ?? throw new ArgumentNullException(nameof(key));
        return this;
    }

    public ModelQueryImpl ModelVersion(int? version)
    {
        if (version == null) throw new ArgumentNullException(nameof(version));
        if (version <= 0) throw new ArgumentException("Version must be positive");
        this.version = version;
        return this;
    }

    public ModelQueryImpl LatestVersion()
    {
        latest = true;
        return this;
    }

    public ModelQueryImpl DeploymentId(string deploymentId)
    {
        this.deploymentId = deploymentId ?? throw new ArgumentNullException(nameof(deploymentId));
        return this;
    }

    public ModelQueryImpl NotDeployed()
    {
        if (deployed) throw new InvalidOperationException("Cannot use deployed() and notDeployed() in the same query");
        notDeployed = true;
        return this;
    }

    public ModelQueryImpl Deployed()
    {
        if (notDeployed) throw new InvalidOperationException("Cannot use deployed() and notDeployed() in the same query");
        deployed = true;
        return this;
    }

    public ModelQueryImpl ModelTenantId(string tenantId)
    {
        this.tenantId = tenantId ?? throw new ArgumentNullException(nameof(tenantId));
        return this;
    }

    public ModelQueryImpl ModelTenantIdLike(string tenantIdLike)
    {
        this.tenantIdLike = tenantIdLike ?? throw new ArgumentNullException(nameof(tenantIdLike));
        return this;
    }

    public ModelQueryImpl ModelWithoutTenantId()
    {
        withoutTenantId = true;
        return this;
    }

    public ModelQueryImpl OrderByModelCategory() => OrderByProperty(ModelQueryProperty.ModelCategory);
    public ModelQueryImpl OrderByModelId() => OrderByProperty(ModelQueryProperty.ModelId);
    public ModelQueryImpl OrderByModelKey() => OrderByProperty(ModelQueryProperty.ModelKey);
    public ModelQueryImpl OrderByModelVersion() => OrderByProperty(ModelQueryProperty.ModelVersion);
    public ModelQueryImpl OrderByModelName() => OrderByProperty(ModelQueryProperty.ModelName);
    public ModelQueryImpl OrderByCreateTime() => OrderByProperty(ModelQueryProperty.ModelCreateTime);
    public ModelQueryImpl OrderByLastUpdateTime() => OrderByProperty(ModelQueryProperty.ModelLastUpdateTime);
    public ModelQueryImpl OrderByTenantId() => OrderByProperty(ModelQueryProperty.ModelTenantId);

    public override Task<List<ModelRecord>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var result = ApplyFilters(_source);
        result = ApplySorting(result);
        result = ApplyIQueryFilters(result);
        if (FirstResultValue.HasValue) result = result.Skip(FirstResultValue.Value);
        if (MaxResultsValue.HasValue) result = result.Take(MaxResultsValue.Value);
        return Task.FromResult(result.ToList());
    }

    public override Task<ModelRecord?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).FirstOrDefault());
    }

    public override Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ApplyFilters(_source).Count());
    }

    private IEnumerable<ModelRecord> ApplyFilters(IEnumerable<ModelRecord>? source)
    {
        if (source == null) return Enumerable.Empty<ModelRecord>();
        var query = source.AsEnumerable();

        if (id != null) query = query.Where(m => m.Id == id);
        if (category != null) query = query.Where(m => m.Category == category);
        if (categoryLike != null) query = query.Where(m => m.Category != null && m.Category.Contains(categoryLike.Replace("%", "")));
        if (categoryNotEquals != null) query = query.Where(m => m.Category != categoryNotEquals);
        if (name != null) query = query.Where(m => m.Name == name);
        if (nameLike != null) query = query.Where(m => m.Name != null && m.Name.Contains(nameLike.Replace("%", "")));
        if (key != null) query = query.Where(m => m.Key == key);
        if (version.HasValue) query = query.Where(m => m.Version == version);
        if (deploymentId != null) query = query.Where(m => m.DeploymentId == deploymentId);
        if (notDeployed) query = query.Where(m => m.DeploymentId == null);
        if (deployed) query = query.Where(m => m.DeploymentId != null);
        if (tenantId != null) query = query.Where(m => m.TenantId == tenantId);
        if (tenantIdLike != null) query = query.Where(m => m.TenantId != null && m.TenantId.Contains(tenantIdLike.Replace("%", "")));
        if (withoutTenantId) query = query.Where(m => m.TenantId == null);

        if (latest)
        {
            var results = query.ToList();
            query = results
                .GroupBy(m => m.Key)
                .Select(g => g.OrderByDescending(m => m.Version).First())
                .AsEnumerable();
        }

        return query;
    }

    private IEnumerable<ModelRecord> ApplySorting(IEnumerable<ModelRecord> query)
    {
        if (OrderProperty == null || SortDirection == null) return query;
        bool asc = SortDirection == Direction.Ascending;
        return OrderProperty switch
        {
            ModelQueryProperty.ModelId => asc ? query.OrderBy(m => m.Id) : query.OrderByDescending(m => m.Id),
            ModelQueryProperty.ModelName => asc ? query.OrderBy(m => m.Name) : query.OrderByDescending(m => m.Name),
            ModelQueryProperty.ModelCategory => asc ? query.OrderBy(m => m.Category) : query.OrderByDescending(m => m.Category),
            ModelQueryProperty.ModelKey => asc ? query.OrderBy(m => m.Key) : query.OrderByDescending(m => m.Key),
            ModelQueryProperty.ModelVersion => asc ? query.OrderBy(m => m.Version) : query.OrderByDescending(m => m.Version),
            ModelQueryProperty.ModelCreateTime => asc ? query.OrderBy(m => m.CreateTime) : query.OrderByDescending(m => m.CreateTime),
            ModelQueryProperty.ModelLastUpdateTime => asc ? query.OrderBy(m => m.LastUpdateTime) : query.OrderByDescending(m => m.LastUpdateTime),
            _ => query
        };
    }
}
