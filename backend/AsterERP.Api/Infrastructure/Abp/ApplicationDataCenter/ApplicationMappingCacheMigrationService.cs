using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDataCenter;

public sealed class ApplicationMappingCacheMigrationService
{
    public async Task MigrateAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        if (!db.DbMaintenance.IsAnyTable("app_dictionary_codes", false))
        {
            return;
        }

        var legacyItems = await db.Queryable<ApplicationDataCenterDictionaryEntity>()
            .Where(item => item.ObjectType == "MappingCache" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var legacyItem in legacyItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await db.Ado.BeginTranAsync();
            try
            {
                await MigrateOneAsync(db, legacyItem, cancellationToken);
                await db.Ado.CommitTranAsync();
            }
            catch
            {
                await db.Ado.RollbackTranAsync();
                throw;
            }
        }
    }

    private static async Task MigrateOneAsync(
        ISqlSugarClient db,
        ApplicationDataCenterDictionaryEntity legacyItem,
        CancellationToken cancellationToken)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(legacyItem.ConfigJson);
        var source = ReadObject(config, "source");
        var dataSourceId = legacyItem.DataSourceId ?? ReadString(config, "dataSourceId") ?? ReadString(source, "dataSourceId");
        var provider = ReadString(source, "provider") ?? ReadString(config, "provider") ?? "";
        var schemaName = ReadString(source, "schemaName") ?? ReadString(config, "schemaName");
        var objectName = ReadString(source, "objectName") ?? ReadString(config, "objectName") ?? ReadString(config, "sourceObjectName");
        var cache = await db.Queryable<ApplicationMappingCacheEntity>()
            .Where(item => item.Id == legacyItem.Id)
            .FirstAsync(cancellationToken);
        var now = DateTime.UtcNow;
        cache ??= new ApplicationMappingCacheEntity
        {
            Id = legacyItem.Id,
            TenantId = legacyItem.TenantId,
            AppCode = legacyItem.AppCode,
            CacheKey = legacyItem.ObjectCode,
            CacheName = legacyItem.ObjectName,
            CreatedBy = legacyItem.CreatedBy,
            CreatedTime = legacyItem.CreatedTime,
            Remark = legacyItem.Remark,
            IsDeleted = false
        };

        cache.TenantId = legacyItem.TenantId;
        cache.AppCode = legacyItem.AppCode;
        cache.DataSourceId = dataSourceId ?? string.Empty;
        cache.CacheKey = string.IsNullOrWhiteSpace(cache.CacheKey) ? legacyItem.ObjectCode : cache.CacheKey;
        cache.CacheName = string.IsNullOrWhiteSpace(cache.CacheName) ? legacyItem.ObjectName : cache.CacheName;
        cache.Provider = provider;
        cache.SchemaName = schemaName;
        cache.ObjectName = objectName;
        cache.VersionNo = Math.Max(cache.VersionNo, legacyItem.VersionNo);
        cache.UpdatedTime = now;
        cache.UpdatedBy = legacyItem.UpdatedBy;

        try
        {
            var catalogTable = await ResolveCatalogTableAsync(db, legacyItem, dataSourceId, schemaName, objectName, cancellationToken);
            if (catalogTable is null)
            {
                SetBlocked(cache, "无法根据目录快照解析 Mapping Cache 的源表。");
            }
            else
            {
                cache.DataSourceId = dataSourceId!;
                cache.SourceResourceId = ResolveTableResourceId(dataSourceId!, catalogTable);
                cache.SchemaName = catalogTable.SchemaName;
                cache.ObjectName = catalogTable.TableName;
                cache.Provider = string.IsNullOrWhiteSpace(provider)
                    ? await ResolveProviderAsync(db, legacyItem, dataSourceId!, cancellationToken)
                    : provider;
                var columns = ResolveColumns(config, source, catalogTable, cache.SourceResourceId);
                if (columns.Count == 0)
                {
                    throw new InvalidOperationException("旧 Mapping Cache 没有可迁移的字段映射。");
                }

                cache.Status = ApplicationDataCenterObjectStatus.Normal;
                cache.LastValidationStatus = "MigrationSucceeded";
                cache.LastValidationMessage = $"Migrated {columns.Count} mapping columns.";
                await UpsertCacheAsync(db, cache, cancellationToken);
                await ReplaceChildrenAsync(db, cache, columns, ResolveParameters(config, source, columns), cancellationToken);
                return;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            SetBlocked(cache, exception.Message);
        }

        await UpsertCacheAsync(db, cache, cancellationToken);
    }

    private static async Task UpsertCacheAsync(ISqlSugarClient db, ApplicationMappingCacheEntity cache, CancellationToken cancellationToken)
    {
        var exists = await db.Queryable<ApplicationMappingCacheEntity>().Where(item => item.Id == cache.Id).AnyAsync(cancellationToken);
        if (exists)
        {
            await db.Updateable(cache).Where(item => item.Id == cache.Id).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Insertable(cache).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task ReplaceChildrenAsync(
        ISqlSugarClient db,
        ApplicationMappingCacheEntity cache,
        IReadOnlyList<MigrationColumn> columns,
        IReadOnlyList<MigrationParameter> parameters,
        CancellationToken cancellationToken)
    {
        await db.Deleteable<ApplicationMappingCacheColumnEntity>().Where(item => item.CacheId == cache.Id).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ApplicationMappingCacheParameterEntity>().Where(item => item.CacheId == cache.Id).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(columns.Select((item, index) => new ApplicationMappingCacheColumnEntity
        {
            TenantId = cache.TenantId,
            AppCode = cache.AppCode,
            CacheId = cache.Id,
            SourceResourceId = item.SourceResourceId,
            TargetName = item.TargetName,
            DataType = item.DataType,
            Nullable = item.Nullable,
            Ordinal = index + 1
        }).ToArray()).ExecuteCommandAsync(cancellationToken);
        if (parameters.Count > 0)
        {
            await db.Insertable(parameters.Select(item => new ApplicationMappingCacheParameterEntity
            {
                TenantId = cache.TenantId,
                AppCode = cache.AppCode,
                CacheId = cache.Id,
                ResourceId = ApplicationDataResourceId.MappingCacheParameter(cache.Id, item.Name),
                Name = item.Name,
                ColumnResourceId = item.ColumnResourceId,
                DataType = item.DataType,
                Required = item.Required,
                DefaultValueJson = item.DefaultValue is null ? null : JsonSerializer.Serialize(item.DefaultValue)
            }).ToArray()).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static async Task<ApplicationDataSourceCatalogTableResponse?> ResolveCatalogTableAsync(
        ISqlSugarClient db,
        ApplicationDataCenterDictionaryEntity legacyItem,
        string? dataSourceId,
        string? schemaName,
        string? objectName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId) || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var snapshot = await db.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item => item.TenantId == legacyItem.TenantId && item.AppCode == legacyItem.AppCode && item.DataSourceId == dataSourceId)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        var tables = string.IsNullOrWhiteSpace(snapshot?.CatalogJson)
            ? []
            : JsonSerializer.Deserialize<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>>(snapshot.CatalogJson, ApplicationDataCenterJson.Options) ?? [];
        return tables.FirstOrDefault(item =>
            string.Equals(item.TableName, objectName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SchemaName ?? string.Empty, schemaName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<MigrationColumn> ResolveColumns(
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, object?> source,
        ApplicationDataSourceCatalogTableResponse table,
        string tableResourceId)
    {
        var raw = ReadArray(source, "columns").Count > 0 ? ReadArray(source, "columns") : ReadArray(config, "columns");
        if (raw.Count == 0)
        {
            raw = ReadArray(config, "fieldMappings");
        }

        var result = new List<MigrationColumn>();
        foreach (var value in raw)
        {
            var sourceResourceId = ReadString(value, "sourceResourceId") ?? ReadString(value, "resourceId");
            var sourceName = ReadString(value, "sourceName") ?? ReadString(value, "columnName") ?? ReadString(value, "fieldCode");
            var catalogColumn = table.Columns.FirstOrDefault(item =>
                (!string.IsNullOrWhiteSpace(sourceResourceId) && string.Equals(item.ResourceId, sourceResourceId, StringComparison.Ordinal)) ||
                (!string.IsNullOrWhiteSpace(sourceName) && string.Equals(item.ColumnName, sourceName, StringComparison.OrdinalIgnoreCase)));
            if (catalogColumn is null)
            {
                continue;
            }

            result.Add(new(
                string.IsNullOrWhiteSpace(catalogColumn.ResourceId) ? ApplicationDataResourceId.Column(tableResourceId, catalogColumn.ColumnName) : catalogColumn.ResourceId,
                ReadString(value, "targetName") ?? ReadString(value, "targetField") ?? catalogColumn.ColumnName,
                string.IsNullOrWhiteSpace(catalogColumn.DataType) ? "string" : catalogColumn.DataType,
                catalogColumn.Nullable));
        }

        return result.GroupBy(item => item.SourceResourceId, StringComparer.Ordinal).Select(item => item.First()).ToArray();
    }

    private static IReadOnlyList<MigrationParameter> ResolveParameters(
        IReadOnlyDictionary<string, object?> config,
        IReadOnlyDictionary<string, object?> source,
        IReadOnlyList<MigrationColumn> columns)
    {
        var raw = ReadArray(source, "parameters").Count > 0 ? ReadArray(source, "parameters") : ReadArray(config, "parameters");
        return raw.Select(value =>
        {
            var columnResourceId = ReadString(value, "columnResourceId") ?? ReadString(value, "sourceResourceId");
            var column = columns.FirstOrDefault(item => string.Equals(item.SourceResourceId, columnResourceId, StringComparison.Ordinal));
            var name = ReadString(value, "name") ?? ReadString(value, "parameterName");
            return column is null || string.IsNullOrWhiteSpace(name)
                ? null
                : new MigrationParameter(name, column.SourceResourceId, ReadString(value, "dataType") ?? column.DataType, ReadBoolean(value, "required", true), ReadValue(value, "defaultValue"));
        }).Where(item => item is not null).Select(item => item!).ToArray();
    }

    private static string ResolveTableResourceId(string dataSourceId, ApplicationDataSourceCatalogTableResponse table) =>
        string.IsNullOrWhiteSpace(table.ResourceId) ? ApplicationDataResourceId.Table(dataSourceId, table.SchemaName, table.TableName) : table.ResourceId;

    private static async Task<string> ResolveProviderAsync(
        ISqlSugarClient db,
        ApplicationDataCenterDictionaryEntity legacyItem,
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        var source = await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.TenantId == legacyItem.TenantId && item.AppCode == legacyItem.AppCode && item.Id == dataSourceId && !item.IsDeleted)
            .FirstAsync(cancellationToken);
        return source?.ObjectType ?? string.Empty;
    }

    private static void SetBlocked(ApplicationMappingCacheEntity cache, string message)
    {
        cache.Status = ApplicationDataCenterObjectStatus.MigrationBlocked;
        cache.LastValidationStatus = ApplicationDataCenterObjectStatus.MigrationBlocked;
        cache.LastValidationMessage = message[..Math.Min(message.Length, 2000)];
    }

    private static IReadOnlyDictionary<string, object?> ReadObject(IReadOnlyDictionary<string, object?> parent, string key) =>
        parent.TryGetValue(key, out var value) && value is JsonElement element && element.ValueKind == JsonValueKind.Object
            ? element.Deserialize<Dictionary<string, object?>>(ApplicationDataCenterJson.Options) ?? []
            : [];

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadArray(IReadOnlyDictionary<string, object?> parent, string key) =>
        parent.TryGetValue(key, out var value) && value is JsonElement element && element.ValueKind == JsonValueKind.Array
            ? element.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(item => item.Deserialize<Dictionary<string, object?>>(ApplicationDataCenterJson.Options) ?? []).ToArray()
            : [];

    private static string? ReadString(IReadOnlyDictionary<string, object?> parent, string key) =>
        parent.TryGetValue(key, out var value) ? ReadValueString(value) : null;

    private static string? ReadString(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var property) ? ReadValueString(property) : null;

    private static string? ReadValueString(object? value) => value switch
    {
        JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
        JsonElement element when element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
        _ => value?.ToString()
    };

    private static object? ReadValue(IReadOnlyDictionary<string, object?> value, string key) => value.TryGetValue(key, out var result) ? result is JsonElement element ? element.Clone() : result : null;

    private static bool ReadBoolean(JsonElement value, string key, bool fallback) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var property) && property.ValueKind is JsonValueKind.True or JsonValueKind.False ? property.GetBoolean() : fallback;

    private static bool ReadBoolean(IReadOnlyDictionary<string, object?> value, string key, bool fallback)
    {
        if (!value.TryGetValue(key, out var raw))
        {
            return fallback;
        }

        if (raw is bool boolean)
        {
            return boolean;
        }

        return bool.TryParse(ReadValueString(raw), out var parsed) ? parsed : fallback;
    }

    private sealed record MigrationColumn(string SourceResourceId, string TargetName, string DataType, bool Nullable);
    private sealed record MigrationParameter(string Name, string ColumnResourceId, string DataType, bool Required, object? DefaultValue);
}
