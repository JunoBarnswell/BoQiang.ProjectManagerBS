using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationQueryPlanResourceResolver(
    IWorkspaceDatabaseAccessor databaseAccessor)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApplicationQueryPlanResolvedResource> ResolveCatalogResourceAsync(
        string dataSourceId,
        string resourceId,
        CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == dataSourceId &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("Data source Resource ID does not exist in the current workspace.", ErrorCodes.ApplicationDataCenterObjectNotFound);
        var snapshot = await db.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item =>
                item.DataSourceId == source.Id &&
                !item.IsDeleted)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .OrderBy(item => item.CapturedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken)
            ?? throw Invalid("Resource ID requires a current catalog snapshot.");
        var table = Deserialize(snapshot.CatalogJson)
            .Select(item => (Table: item, ResourceId: ResolveTableResourceId(source.Id, item)))
            .FirstOrDefault(item => string.Equals(item.ResourceId, resourceId.Trim(), StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(table.ResourceId))
            throw new NotFoundException("Catalog Resource ID does not exist.", ErrorCodes.ApplicationDataCenterObjectNotFound);
        return ToTableResource(source.Id, table.Table, table.ResourceId);
    }

    public async Task<ApplicationQueryPlanResolvedModel> ResolveAsync(
        ApplicationQueryPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.DataSourceId))
            throw Invalid("QueryPlan requires a data-source Resource ID.");
        if (request.Nodes.Count is < 1 or > 20)
            throw Invalid("QueryPlan requires between 1 and 20 Resource ID nodes.");

        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == request.DataSourceId &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("Data source Resource ID does not exist in the current workspace.", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var snapshot = await db.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item =>
                item.DataSourceId == source.Id &&
                !item.IsDeleted)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .OrderBy(item => item.CapturedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken)
            ?? throw Invalid("QueryPlan requires a current catalog snapshot.");

        var catalog = Deserialize(snapshot.CatalogJson);
        var nodes = new List<ApplicationQueryPlanResolvedNode>(request.Nodes.Count);
        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in request.Nodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(node.Id) || !nodeIds.Add(node.Id))
                throw Invalid("QueryPlan node IDs must be unique and non-empty.");
            if (string.IsNullOrWhiteSpace(node.ResourceId))
                throw Invalid("QueryPlan nodes must reference a stable Resource ID.");
            if (!string.IsNullOrWhiteSpace(node.Alias))
                ApplicationDataSourceSqlNamePolicy.RequireIdentifier(node.Alias.Trim(), "node alias");

            var resource = await ResolveResourceAsync(db, source, catalog, node.ResourceId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(node.Kind) &&
                !string.Equals(node.Kind, resource.Kind, StringComparison.OrdinalIgnoreCase))
                throw Invalid($"QueryPlan node kind does not match Resource ID '{node.ResourceId}'.");
            nodes.Add(new(node.Id.Trim(), node.Alias.Trim(), resource));
        }

        return new ApplicationQueryPlanResolvedModel(nodes);
    }

    private async Task<ApplicationQueryPlanResolvedResource> ResolveResourceAsync(
        ISqlSugarClient db,
        ApplicationDataSourceEntity source,
        IReadOnlyList<ApplicationDataSourceCatalogTableResponse> catalog,
        string resourceId,
        CancellationToken cancellationToken)
    {
        var table = catalog
            .Select(item => (Table: item, ResourceId: ResolveTableResourceId(source.Id, item)))
            .FirstOrDefault(item => string.Equals(item.ResourceId, resourceId.Trim(), StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(table.ResourceId))
            return ToTableResource(source.Id, table.Table, table.ResourceId);

        var cache = await db.Queryable<ApplicationMappingCacheEntity>()
            .Where(item =>
                item.Id == resourceId &&
                item.DataSourceId == source.Id &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("QueryPlan Resource ID is not present in the current catalog or mapping cache.", ErrorCodes.ApplicationDataCenterObjectNotFound);

        if (string.IsNullOrWhiteSpace(cache.SourceResourceId))
            throw Invalid("Mapping cache is missing its source Resource ID and cannot be used by QueryPlan.");
        var sourceTable = catalog
            .Select(item => (Table: item, ResourceId: ResolveTableResourceId(source.Id, item)))
            .FirstOrDefault(item => string.Equals(item.ResourceId, cache.SourceResourceId, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(sourceTable.ResourceId))
            throw Invalid("Mapping cache source Resource ID is not present in the current catalog snapshot.");

        var sourceFields = ToTableResource(source.Id, sourceTable.Table, sourceTable.ResourceId).Fields
            .ToDictionary(item => item.ResourceId, StringComparer.OrdinalIgnoreCase);
        var columns = await db.Queryable<ApplicationMappingCacheColumnEntity>()
            .Where(item =>
                item.CacheId == cache.Id &&
                !item.IsDeleted)
            .OrderBy(item => item.Ordinal)
            .ToListAsync(cancellationToken);
        if (columns.Count == 0)
            throw Invalid("Mapping cache must contain at least one Resource ID column mapping.");

        var fields = new List<ApplicationQueryPlanResolvedField>(columns.Count);
        foreach (var column in columns)
        {
            if (!sourceFields.TryGetValue(column.SourceResourceId, out var sourceField))
                throw Invalid("Mapping cache contains a source column Resource ID outside the current catalog.");
            var targetResourceId = ApplicationDataResourceId.MappingCacheColumn(cache.Id, column.TargetName);
            fields.Add(new(targetResourceId, column.TargetName, column.DataType, column.Nullable, sourceField.ResourceId, sourceField.SourceName));
        }

        var parameters = await db.Queryable<ApplicationMappingCacheParameterEntity>()
            .Where(item =>
                item.CacheId == cache.Id &&
                !item.IsDeleted)
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var resolvedParameters = parameters.Select(item =>
        {
            if (string.IsNullOrWhiteSpace(item.ResourceId))
                throw Invalid("Mapping cache parameter is missing a stable Resource ID.");
            var column = fields.FirstOrDefault(field =>
                string.Equals(field.SourceResourceId, item.ColumnResourceId, StringComparison.OrdinalIgnoreCase));
            if (column is null)
                throw Invalid("Mapping cache parameter references a column Resource ID outside the cache.");
            return new ApplicationQueryPlanResolvedParameter(
                item.ResourceId,
                item.Name,
                NormalizeMappingCacheParameterType(item.DataType),
                item.Required,
                DeserializeDefault(item.DefaultValueJson),
                column.ResourceId,
                column.SourceName);
        }).ToArray();

        return new(
            cache.Id,
            ApplicationDataResourceKind.MappingCache,
            sourceTable.Table.SchemaName,
            sourceTable.Table.TableName,
            fields,
            resolvedParameters,
            sourceTable.ResourceId);
    }

    private static string NormalizeMappingCacheParameterType(string value)
    {
        try { return ApplicationMappingCacheParameterType.Normalize(value); }
        catch (ArgumentException)
        {
            throw Invalid($"Unsupported mapping cache parameter type: {value}.");
        }
    }

    private static ApplicationQueryPlanResolvedResource ToTableResource(
        string dataSourceId,
        ApplicationDataSourceCatalogTableResponse table,
        string resourceId)
    {
        var kind = table.TableType.Equals("VIEW", StringComparison.OrdinalIgnoreCase)
            ? ApplicationDataResourceKind.View
            : ApplicationDataResourceKind.Table;
        var fields = table.Columns.Select(column =>
        {
            var fieldResourceId = string.IsNullOrWhiteSpace(column.ResourceId)
                ? ApplicationDataResourceId.Column(resourceId, column.ColumnName)
                : column.ResourceId;
            return new ApplicationQueryPlanResolvedField(
                fieldResourceId,
                column.ColumnName,
                column.DataType,
                column.Nullable,
                fieldResourceId,
                column.ColumnName);
        }).ToArray();
        return new(resourceId, kind, table.SchemaName, table.TableName, fields, [], resourceId);
    }

    private static string ResolveTableResourceId(string dataSourceId, ApplicationDataSourceCatalogTableResponse table) =>
        string.IsNullOrWhiteSpace(table.ResourceId)
            ? ApplicationDataResourceId.Table(dataSourceId, table.SchemaName, table.TableName)
            : table.ResourceId;

    private static IReadOnlyList<ApplicationDataSourceCatalogTableResponse> Deserialize(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>>(json, JsonOptions) ?? [];

    private static object? DeserializeDefault(string? json) =>
        string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<object>(json, JsonOptions);

    private static ValidationException Invalid(string message) =>
        new(message, ErrorCodes.ApplicationDataCenterInvalidConfig);
}
