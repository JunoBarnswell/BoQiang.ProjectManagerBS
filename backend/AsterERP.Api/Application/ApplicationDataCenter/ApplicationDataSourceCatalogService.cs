using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceCatalogService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataSourceProviderRegistry providerRegistry)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ApplicationDataSourceCatalogSnapshotResponse> RefreshAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default) => RefreshAsync(dataSourceId, null, cancellationToken);

    public async Task<ApplicationDataSourceCatalogSnapshotResponse> RefreshNodeAsync(
        string dataSourceId,
        ApplicationDataSourceCatalogRefreshRequest request,
        CancellationToken cancellationToken = default) =>
        await RefreshAsync(dataSourceId, request, cancellationToken);

    public async Task<ApplicationDataSourceCatalogSnapshotResponse?> GetLatestAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await db.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item => item.DataSourceId == dataSourceId)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .OrderBy(item => item.CapturedAt, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        return entity is null ? null : Map(entity, Deserialize(entity.CatalogJson), DeserializeChanges(entity.ChangeJson));
    }

    private async Task<ApplicationDataSourceCatalogSnapshotResponse> RefreshAsync(
        string dataSourceId,
        ApplicationDataSourceCatalogRefreshRequest? node,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await appDb.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        if (!ApplicationDataSourceConnectionFactory.IsDatabaseType(dataSource.ObjectType))
            throw new ValidationException("当前数据源不是数据库，无法生成目录快照", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (node is not null && string.IsNullOrWhiteSpace(node.TableName))
            throw new ValidationException("节点刷新必须指定表名", ErrorCodes.ApplicationDataCenterInvalidConfig);

        var provider = providerRegistry.Resolve(dataSource.ObjectType);
        var previous = await appDb.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item => item.DataSourceId == dataSourceId)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        var tables = previous is null || node is null
            ? await ReadFullCatalogAsync(dataSource, provider, cancellationToken)
            : await RefreshSingleNodeAsync(dataSource, provider, Deserialize(previous.CatalogJson), node, cancellationToken);
        var catalogJson = JsonSerializer.Serialize(tables, JsonOptions);
        var hash = Hash(catalogJson);
        var changes = BuildChanges(previous is null ? [] : Deserialize(previous.CatalogJson), tables);
        var snapshot = new ApplicationDataSourceCatalogSnapshotEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DataSourceId = dataSourceId,
            Provider = provider.Type,
            SnapshotHash = hash,
            VersionNo = (previous?.VersionNo ?? 0) + 1,
            PreviousSnapshotId = previous?.Id,
            PreviousSnapshotHash = previous?.SnapshotHash,
            CapturedAt = DateTime.UtcNow,
            CatalogJson = catalogJson,
            ChangeJson = JsonSerializer.Serialize(changes, JsonOptions)
        };
        await appDb.Insertable(snapshot).ExecuteCommandAsync(cancellationToken);
        return Map(snapshot, tables, changes);
    }

    private async Task<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>> ReadFullCatalogAsync(
        ApplicationDataSourceEntity dataSource,
        IApplicationDataSourceProvider provider,
        CancellationToken cancellationToken)
    {
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        var tables = await ReadTablesAsync(db, provider, dataSource.Id, cancellationToken);
        var catalog = new List<ApplicationDataSourceCatalogTableResponse>(tables.Count);
        foreach (var table in tables)
            catalog.Add(await ReadTableAsync(db, provider, dataSource.Id, table, cancellationToken));
        return catalog;
    }

    private async Task<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>> RefreshSingleNodeAsync(
        ApplicationDataSourceEntity dataSource,
        IApplicationDataSourceProvider provider,
        IReadOnlyList<ApplicationDataSourceCatalogTableResponse> previous,
        ApplicationDataSourceCatalogRefreshRequest request,
        CancellationToken cancellationToken)
    {
        var existing = previous.FirstOrDefault(item =>
            string.Equals(item.TableName, request.TableName.Trim(), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SchemaName ?? string.Empty, request.SchemaName?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
            throw new NotFoundException("目录节点不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        var table = new ApplicationDataSourceTableResponse(existing.TableName, existing.SchemaName, existing.TableType)
        {
            ResourceId = string.IsNullOrWhiteSpace(existing.ResourceId)
                ? ApplicationDataResourceId.Table(dataSource.Id, existing.SchemaName, existing.TableName)
                : existing.ResourceId
        };
        var refreshed = await ReadTableAsync(db, provider, dataSource.Id, table, cancellationToken);
        return previous.Select(item => ReferenceEquals(item, existing) ? refreshed : item).ToArray();
    }

    private static async Task<ApplicationDataSourceCatalogTableResponse> ReadTableAsync(
        ISqlSugarClient db,
        IApplicationDataSourceProvider provider,
        string dataSourceId,
        ApplicationDataSourceTableResponse table,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(
            table.TableName,
            table.SchemaName,
            table.TableType,
            await ReadColumnsAsync(db, provider, table, cancellationToken),
            await ReadObjectsAsync(db, provider.Catalog.ConstraintsSql, table, cancellationToken),
            await ReadObjectsAsync(db, provider.Catalog.IndexesSql, table, cancellationToken),
            await ReadObjectsAsync(db, provider.Catalog.TriggersSql, table, cancellationToken),
            await ReadObjectsAsync(db, provider.Catalog.CommentsSql, table, cancellationToken))
        {
            ResourceId = string.IsNullOrWhiteSpace(table.ResourceId)
                ? ApplicationDataResourceId.Table(dataSourceId, table.SchemaName, table.TableName)
                : table.ResourceId
        };
    }

    private static async Task<IReadOnlyList<ApplicationDataSourceTableResponse>> ReadTablesAsync(
        ISqlSugarClient db,
        IApplicationDataSourceProvider provider,
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        var table = await ExecuteDataTableAsync(db, provider.Catalog.TablesSql, [], cancellationToken);
        return table.Rows.Cast<DataRow>()
            .Select(row =>
            {
                var tableName = Cell(row, "TableName");
                var schemaName = NullCell(row, "SchemaName");
                return new ApplicationDataSourceTableResponse(tableName, schemaName, NullCell(row, "TableType") ?? "TABLE")
                {
                    ResourceId = ApplicationDataResourceId.Table(dataSourceId, schemaName, tableName)
                };
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<ApplicationDataSourceCatalogColumnResponse>> ReadColumnsAsync(
        ISqlSugarClient db,
        IApplicationDataSourceProvider provider,
        ApplicationDataSourceTableResponse table,
        CancellationToken cancellationToken)
    {
        var dataTable = await ExecuteDataTableAsync(db, provider.Catalog.ColumnsSql, Parameters(table), cancellationToken);
        return dataTable.Rows.Cast<DataRow>().Select(row =>
        {
            var dataType = NullCell(row, "DataType") ?? "TEXT";
            var name = Cell(row, "ColumnName");
            return new ApplicationDataSourceCatalogColumnResponse(
                name,
                dataType,
                ConvertBoolean(row, "Nullable"),
                ConvertBoolean(row, "PrimaryKey"),
                Convert.ToInt32(row["OrdinalPosition"]),
                IsVersionColumn(name, dataType) ? "version" : null,
                NullCell(row, "Comment"))
            {
                ResourceId = ApplicationDataResourceId.Column(table.ResourceId, name)
            };
        }).OrderBy(item => item.Order).ToArray();
    }

    private static async Task<IReadOnlyList<ApplicationDataSourceCatalogObjectResponse>> ReadObjectsAsync(
        ISqlSugarClient db,
        string sql,
        ApplicationDataSourceTableResponse table,
        CancellationToken cancellationToken)
    {
        var dataTable = await ExecuteDataTableAsync(db, sql, Parameters(table), cancellationToken);
        return dataTable.Rows.Cast<DataRow>()
            .Select(row => new ApplicationDataSourceCatalogObjectResponse(
                FirstCell(row, "ObjectName", "ConstraintName", "IndexName", "TriggerName"),
                NullCell(row, "ObjectType", "ConstraintType", "IndexType"),
                NullCell(row, "Definition", "sql", "ActionStatement")))
            .ToArray();
    }

    private static IReadOnlyList<ApplicationDataSourceCatalogChangeResponse> BuildChanges(
        IReadOnlyList<ApplicationDataSourceCatalogTableResponse> previous,
        IReadOnlyList<ApplicationDataSourceCatalogTableResponse> current)
    {
        var changes = new List<ApplicationDataSourceCatalogChangeResponse>();
        var oldNodes = BuildStableNodeMap(previous);
        var newNodes = BuildStableNodeMap(current);
        foreach (var key in oldNodes.Keys.Except(newNodes.Keys, StringComparer.OrdinalIgnoreCase))
            changes.Add(new("Removed", oldNodes[key].NodeType, oldNodes[key].Name, oldNodes[key].Schema, null));
        foreach (var key in newNodes.Keys.Except(oldNodes.Keys, StringComparer.OrdinalIgnoreCase))
            changes.Add(new("Added", newNodes[key].NodeType, newNodes[key].Name, newNodes[key].Schema, null));
        foreach (var key in oldNodes.Keys.Intersect(newNodes.Keys, StringComparer.OrdinalIgnoreCase))
            if (!string.Equals(oldNodes[key].Detail, newNodes[key].Detail, StringComparison.Ordinal))
                changes.Add(new("Changed", newNodes[key].NodeType, newNodes[key].Name, newNodes[key].Schema, newNodes[key].Detail));
        return changes;
    }

    private static IReadOnlyDictionary<string, (string NodeType, string Name, string? Schema, string Detail)> BuildStableNodeMap(
        IReadOnlyList<ApplicationDataSourceCatalogTableResponse> tables)
    {
        return Flatten(tables)
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.Detail.Length)
                    .ThenBy(item => item.Detail, StringComparer.Ordinal)
                    .Select(item => (item.NodeType, item.Name, item.Schema, item.Detail))
                    .First(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<(string Key, string NodeType, string Name, string? Schema, string Detail)> Flatten(IReadOnlyList<ApplicationDataSourceCatalogTableResponse> tables)
    {
        foreach (var table in tables)
        {
            var tableKey = $"table:{table.SchemaName}:{table.TableName}";
            yield return (tableKey, table.TableType, table.TableName, table.SchemaName, JsonSerializer.Serialize(table, JsonOptions));
            foreach (var column in table.Columns)
                yield return ($"{tableKey}:column:{column.ColumnName}", "column", column.ColumnName, table.SchemaName, JsonSerializer.Serialize(column, JsonOptions));
            foreach (var item in table.Constraints.Concat(table.Indexes).Concat(table.Triggers).Concat(table.Comments ?? []))
                yield return ($"{tableKey}:{item.Type}:{item.Name}", item.Type ?? "object", item.Name, table.SchemaName, JsonSerializer.Serialize(item, JsonOptions));
        }
    }

    private static ApplicationDataSourceCatalogSnapshotResponse Map(
        ApplicationDataSourceCatalogSnapshotEntity entity,
        IReadOnlyList<ApplicationDataSourceCatalogTableResponse> tables,
        IReadOnlyList<ApplicationDataSourceCatalogChangeResponse> changes) =>
        new(entity.Id, entity.DataSourceId, entity.Provider, entity.SnapshotHash, entity.CapturedAt, tables)
        {
            VersionNo = entity.VersionNo,
            PreviousSnapshotId = entity.PreviousSnapshotId,
            PreviousSnapshotHash = entity.PreviousSnapshotHash,
            Changes = changes
        };

    private static IReadOnlyList<ApplicationDataSourceCatalogTableResponse> Deserialize(string json) =>
        JsonSerializer.Deserialize<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>>(json, JsonOptions) ?? [];

    private static IReadOnlyList<ApplicationDataSourceCatalogChangeResponse> DeserializeChanges(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyList<ApplicationDataSourceCatalogChangeResponse>>(json, JsonOptions) ?? [];

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    private static SugarParameter[] Parameters(ApplicationDataSourceTableResponse table) => [new("@table", table.TableName), new("@schema", table.SchemaName)];

    private static async Task<DataTable> ExecuteDataTableAsync(
        ISqlSugarClient db,
        string sql,
        IReadOnlyList<SugarParameter> parameters,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步结果读取");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.ParameterName;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        for (var index = 0; index < reader.FieldCount; index++)
            table.Columns.Add(reader.GetName(index), reader.GetFieldType(index));
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = table.NewRow();
            for (var index = 0; index < reader.FieldCount; index++)
                row[index] = await reader.IsDBNullAsync(index, cancellationToken) ? DBNull.Value : reader.GetValue(index);
            table.Rows.Add(row);
        }

        return table;
    }
    private static string Cell(DataRow row, string name) => Convert.ToString(row[name]) ?? string.Empty;
    private static string? NullCell(DataRow row, params string[] names) => names.Select(name => row.Table.Columns.Contains(name) ? Convert.ToString(row[name]) : null).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    private static string FirstCell(DataRow row, params string[] names) => NullCell(row, names) ?? "unknown";
    private static bool ConvertBoolean(DataRow row, string name) => row.Table.Columns.Contains(name) && row[name] != DBNull.Value && Convert.ToBoolean(row[name]);
    private static bool IsVersionColumn(string name, string dataType)
    {
        var normalizedName = name.Trim().ToLowerInvariant();
        var normalizedType = dataType.Trim().ToLowerInvariant();
        return normalizedName is "rowversion" or "xmin" or "version" or "row_version" || normalizedType is "rowversion" or "timestamp" or "xid" || normalizedType.Contains("rowversion", StringComparison.Ordinal);
    }
}
