using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMappingCacheWorkbenchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataSourceProviderRegistry providerRegistry,
    ICurrentUser currentUser,
    ApplicationQueryPlanResourceResolver? resourceResolver = null,
    ApplicationDataCenterSqlScriptAuditWriter? auditWriter = null)
{
    public async Task<IReadOnlyList<ApplicationMappingCacheResponse>> GetListAsync(string dataSourceId, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.AppDataCenterMappingCacheView);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await RequireDataSourceAsync(db, dataSourceId, cancellationToken);
        var caches = await QueryCaches(db, dataSourceId).OrderBy(item => item.UpdatedTime, OrderByType.Desc).ToListAsync(cancellationToken);
        return await Task.WhenAll(caches.Select(item => MapAsync(db, workspace, item, cancellationToken)));
    }

    public async Task<ApplicationMappingCacheResponse> CreateAsync(string dataSourceId, ApplicationMappingCacheUpsertRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.AppDataCenterMappingCacheAdd);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = await RequireDataSourceAsync(db, dataSourceId, cancellationToken);
        var definition = await ValidateAsync(source, request, cancellationToken);
        await EnsureCodeUniqueAsync(db, definition.Target.CacheKey, null, cancellationToken);
        var entity = new ApplicationMappingCacheEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DataSourceId = source.Id,
            CacheKey = definition.Target.CacheKey,
            CacheName = definition.Target.CacheName,
            Provider = definition.Source.Provider,
            SchemaName = definition.Source.SchemaName,
            SourceResourceId = definition.Source.ResourceId,
            ObjectName = null,
            Remark = NormalizeRemark(request.Remark),
            CreatedBy = workspace.UserId
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await ReplaceChildrenAsync(db, entity, definition, cancellationToken);
        return await MapAsync(db, workspace, entity, cancellationToken);
    }

    public async Task<ApplicationMappingCacheResponse> UpdateAsync(string dataSourceId, string cacheId, ApplicationMappingCacheUpsertRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.AppDataCenterMappingCacheEdit);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = await RequireDataSourceAsync(db, dataSourceId, cancellationToken);
        var entity = await RequireCacheAsync(db, dataSourceId, cacheId, cancellationToken);
        var definition = await ValidateAsync(source, request, cancellationToken);
        await EnsureCodeUniqueAsync(db, definition.Target.CacheKey, cacheId, cancellationToken);
        entity.CacheKey = definition.Target.CacheKey;
        entity.CacheName = definition.Target.CacheName;
        entity.Provider = definition.Source.Provider;
        entity.SchemaName = definition.Source.SchemaName;
        entity.SourceResourceId = definition.Source.ResourceId;
        entity.ObjectName = null;
        entity.Remark = NormalizeRemark(request.Remark);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        entity.VersionNo++;
        await db.Updateable(entity).Where(item => item.Id == cacheId).ExecuteCommandAsync(cancellationToken);
        await ReplaceChildrenAsync(db, entity, definition, cancellationToken);
        return await MapAsync(db, workspace, entity, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string dataSourceId, string cacheId, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.AppDataCenterMappingCacheDelete);
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await RequireCacheAsync(db, dataSourceId, cacheId, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedBy = workspace.UserId;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).Where(item => item.Id == cacheId).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<ApplicationMappingCacheTestResponse> TestAsync(string dataSourceId, string cacheId, ApplicationMappingCacheTestRequest request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.AppDataCenterMappingCacheTest);
        var result = await ExecuteAsync(dataSourceId, cacheId, request.Parameters, request.MaxRows, request.TimeoutSeconds, false, cancellationToken);
        return new(result.Success, result.Message, result.DurationMs, result.Fields, result.Rows);
    }

    public Task<ApplicationMappingCacheRefreshResponse> RefreshAsync(string dataSourceId, string cacheId, CancellationToken cancellationToken = default) =>
        RefreshAsync(dataSourceId, cacheId, null, cancellationToken);

    public async Task<ApplicationMappingCacheRefreshResponse> RefreshAsync(string dataSourceId, string cacheId, ApplicationMappingCacheTestRequest? request, CancellationToken cancellationToken = default)
    {
        EnsurePermission(PermissionCodes.AppDataCenterMappingCacheRefresh);
        var result = await ExecuteAsync(dataSourceId, cacheId, request?.Parameters, 500, request?.TimeoutSeconds ?? 30, true, cancellationToken);
        return new(result.Success, result.Message, result.Rows.Count, DateTime.UtcNow, result.Rows);
    }

    private async Task<MappingCacheExecution> ExecuteAsync(string dataSourceId, string cacheId, IReadOnlyDictionary<string, object?>? values, int maxRows, int timeoutSeconds, bool persistRefresh, CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = await RequireDataSourceAsync(appDb, dataSourceId, cancellationToken);
        var cache = await RequireCacheAsync(appDb, dataSourceId, cacheId, cancellationToken);
        var definition = await LoadDefinitionAsync(appDb, cache, cancellationToken);
        var provider = providerRegistry.Resolve(source.ObjectType);
        ValidateProvider(provider, definition);
        var parameters = BuildParameters(definition.Parameters, values);
        var sql = BuildStructuredSelect(provider, definition, parameters);
        var timeout = Math.Clamp(timeoutSeconds <= 0 ? 30 : timeoutSeconds, 1, 300);
        var limitedRows = Math.Clamp(maxRows <= 0 ? 20 : maxRows, 1, provider.Capability.MaxPreviewRows);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await RequireAuditAsync(cancellationToken);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(TimeSpan.FromSeconds(timeout));
            using var sourceDb = await connectionFactory.CreateDatabaseClientAsync(source, linked.Token);
            sourceDb.Ado.CommandTimeOut = timeout;
            var readOnlySql = ApplicationDataSourceSqlPolicy.RequireSelectSql(sql, "Mapping Cache SQL");
            var table = await ExecuteDataTableAsync(sourceDb, provider.BuildPreviewSql(readOnlySql, limitedRows), parameters.Values.ToArray(), linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            stopwatch.Stop();
            var preview = ApplicationDataSourcePreviewMapper.Map(table, persistRefresh ? "Mapping cache refreshed" : "Mapping cache test passed");
            cache.LastValidationStatus = ApplicationDataCenterObjectStatus.Normal;
            cache.LastValidationMessage = preview.Message;
            cache.LastValidatedAt = DateTime.UtcNow;
            if (persistRefresh)
            {
                cache.LastRefreshedAt = cache.LastValidatedAt;
                cache.LastRowCount = preview.Rows.Count;
                cache.Status = ApplicationDataCenterObjectStatus.Normal;
            }
            await appDb.Updateable(cache).Where(item => item.Id == cache.Id).ExecuteCommandAsync(cancellationToken);
            await WriteAuditAsync(cache, source, sql, timeout, stopwatch.ElapsedMilliseconds, preview.Rows.Count, true, null, false, cancellationToken);
            return new(true, preview.Message ?? string.Empty, stopwatch.ElapsedMilliseconds, preview.Fields, preview.Rows);
        }
        catch (OperationCanceledException exception)
        {
            stopwatch.Stop();
            await WriteAuditAsync(cache, source, sql, timeout, stopwatch.ElapsedMilliseconds, 0, false, exception.Message, true, CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            cache.LastValidationStatus = ApplicationDataCenterObjectStatus.Error;
            cache.LastValidationMessage = exception.Message;
            cache.LastValidatedAt = DateTime.UtcNow;
            await appDb.Updateable(cache).Where(item => item.Id == cache.Id).ExecuteCommandAsync(cancellationToken);
            await WriteAuditAsync(cache, source, sql, timeout, stopwatch.ElapsedMilliseconds, 0, false, exception.Message, false, cancellationToken);
            return new(false, exception.Message, stopwatch.ElapsedMilliseconds, [], []);
        }
    }

    private async Task<MappingCacheDefinition> ValidateAsync(ApplicationDataSourceEntity source, ApplicationMappingCacheUpsertRequest request, CancellationToken cancellationToken)
    {
        var provider = providerRegistry.Resolve(source.ObjectType);
        if (!string.Equals(request.Source.DataSourceId, source.Id, StringComparison.Ordinal) || !string.Equals(request.Source.Provider, provider.Type, StringComparison.OrdinalIgnoreCase))
            throw Invalid("Mapping cache source must use the routed data-source Resource ID and provider capability.");
        var resolvedSource = await ResourceResolver.ResolveCatalogResourceAsync(source.Id, request.Source.ResourceId, cancellationToken);
        if (resolvedSource.Kind is not (ApplicationDataResourceKind.Table or ApplicationDataResourceKind.View) || !provider.Capability.SupportsStructuredMappingCache)
            throw Invalid("Mapping cache source must resolve to a supported catalog table or view.");
        var columns = request.Columns.OrderBy(item => item.Ordinal).ToArray();
        if (columns.Length is < 1 or > 100 || columns.Length > provider.Capability.MaxMappingCacheColumns)
            throw Invalid("Mapping cache columns exceed provider capability.");
        var fields = resolvedSource.Fields.ToDictionary(item => item.ResourceId, StringComparer.Ordinal);
        if (columns.Select(item => item.TargetName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Length)
            throw Invalid("Mapping cache target columns must be unique.");
        foreach (var column in columns)
        {
            if (!fields.ContainsKey(column.SourceResourceId))
                throw Invalid("Mapping cache columns must reference catalog field Resource IDs.");
            RequireIdentifier(column.TargetName, "target column");
            RequireType(column.DataType);
        }
        var parameters = new List<ApplicationMappingCacheParameter>();
        if (!provider.Capability.SupportsMappingCacheParameters || (request.Parameters?.Count ?? 0) > provider.Capability.MaxMappingCacheParameters)
            throw Invalid("Mapping cache parameters exceed provider capability.");
        var selected = columns.Select(item => item.SourceResourceId).ToHashSet(StringComparer.Ordinal);
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (parameter, index) in (request.Parameters ?? []).Select((item, index) => (item, index)))
        {
            var parameterName = RequireIdentifier(parameter.Name, $"parameters[{index}].name");
            if (!parameterNames.Add(parameterName))
                throw Invalid($"Mapping cache parameter name is duplicated: {parameterName}.");
            if (!selected.Contains(parameter.ColumnResourceId))
                throw Invalid($"Mapping cache parameter columnResourceId is not selected: parameters[{index}].columnResourceId.");
            var column = fields[parameter.ColumnResourceId];
            string parameterType;
            try
            {
                parameterType = ApplicationMappingCacheParameterType.Normalize(parameter.DataType);
                if (!ApplicationMappingCacheParameterType.IsCompatible(column.DataType, parameterType))
                    throw Invalid($"Mapping cache parameter type is incompatible with its column: parameters[{index}].dataType.");
            }
            catch (ArgumentException)
            {
                throw Invalid($"Unsupported Mapping Cache parameter type at parameters[{index}].dataType.");
            }

            var defaultValue = parameter.DefaultValue is null
                ? null
                : ConvertValue(parameterType, parameter.DefaultValue, $"parameters[{index}].defaultValue");
            parameters.Add(new ApplicationMappingCacheParameter(string.Empty, parameterName, parameter.ColumnResourceId, parameterType, parameter.Required, defaultValue));
        }
        return new(
            request.Source with { DataSourceId = source.Id, ResourceId = resolvedSource.ResourceId, SchemaName = resolvedSource.SchemaName, Provider = provider.Type },
            new(ApplicationDataCenterCodePolicy.NormalizeCode(request.CacheKey, "mapping cache key"), ApplicationDataCenterCodePolicy.NormalizeName(request.CacheName, "mapping cache name")),
            columns,
            parameters,
            resolvedSource);
    }

    private async Task ReplaceChildrenAsync(ISqlSugarClient db, ApplicationMappingCacheEntity entity, MappingCacheDefinition definition, CancellationToken cancellationToken)
    {
        await db.Deleteable<ApplicationMappingCacheColumnEntity>().Where(item => item.CacheId == entity.Id).ExecuteCommandAsync(cancellationToken);
        await db.Deleteable<ApplicationMappingCacheParameterEntity>().Where(item => item.CacheId == entity.Id).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(definition.Columns.Select((item, index) => new ApplicationMappingCacheColumnEntity
        {
            TenantId = entity.TenantId, AppCode = entity.AppCode, CacheId = entity.Id, SourceResourceId = item.SourceResourceId, TargetName = item.TargetName, DataType = item.DataType, Nullable = item.Nullable, Ordinal = index + 1
        }).ToArray()).ExecuteCommandAsync(cancellationToken);
        await db.Insertable(definition.Parameters.Select(item => new ApplicationMappingCacheParameterEntity
        {
            TenantId = entity.TenantId, AppCode = entity.AppCode, CacheId = entity.Id, ResourceId = ApplicationDataResourceId.MappingCacheParameter(entity.Id, item.Name), Name = item.Name, ColumnResourceId = item.ColumnResourceId, DataType = item.DataType, Required = item.Required, DefaultValueJson = item.DefaultValue is null ? null : JsonSerializer.Serialize(item.DefaultValue)
        }).ToArray()).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<MappingCacheDefinition> LoadDefinitionAsync(ISqlSugarClient db, ApplicationMappingCacheEntity entity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.SourceResourceId))
            throw Invalid("Mapping cache has no source Resource ID.");
        var source = await ResourceResolver.ResolveCatalogResourceAsync(entity.DataSourceId, entity.SourceResourceId, cancellationToken);
        var columns = await db.Queryable<ApplicationMappingCacheColumnEntity>().Where(item => item.CacheId == entity.Id && !item.IsDeleted).OrderBy(item => item.Ordinal).ToListAsync(cancellationToken);
        var parameters = await db.Queryable<ApplicationMappingCacheParameterEntity>().Where(item => item.CacheId == entity.Id && !item.IsDeleted).OrderBy(item => item.Name).ToListAsync(cancellationToken);
        return new(
            new(entity.DataSourceId, entity.SourceResourceId, source.SchemaName, entity.Provider),
            new(entity.CacheKey, entity.CacheName),
            columns.Select(item => new ApplicationMappingCacheColumn(item.SourceResourceId, item.TargetName, item.DataType, item.Nullable, item.Ordinal)).ToArray(),
            parameters.Select(item => new ApplicationMappingCacheParameter(item.ResourceId, item.Name, item.ColumnResourceId, NormalizeStoredType(item.DataType), item.Required, DeserializeDefault(item.DefaultValueJson))).ToArray(),
            source);
    }

    private static string BuildStructuredSelect(IApplicationDataSourceProvider provider, MappingCacheDefinition definition, IReadOnlyDictionary<string, SugarParameter> parameters)
    {
        var fields = definition.ResolvedSource.Fields.ToDictionary(item => item.ResourceId, StringComparer.Ordinal);
        var columns = string.Join(", ", definition.Columns.Select(item =>
        {
            if (!fields.TryGetValue(item.SourceResourceId, out var field))
                throw Invalid("Mapping cache column is outside the current catalog.");
            var source = provider.QuoteIdentifier(field.Name);
            var target = provider.QuoteIdentifier(item.TargetName);
            return string.Equals(field.Name, item.TargetName, StringComparison.OrdinalIgnoreCase) ? source : $"{source} AS {target}";
        }));
        var sourceObject = provider.QuoteQualified(definition.ResolvedSource.SchemaName, definition.ResolvedSource.ObjectName);
        var where = definition.Parameters.Count == 0
            ? string.Empty
            : " WHERE " + string.Join(" AND ", definition.Parameters.Select(item =>
            {
                if (!fields.TryGetValue(item.ColumnResourceId, out var field))
                    throw Invalid("Mapping cache parameter is outside the current catalog.");
                return $"{provider.QuoteIdentifier(field.Name)} = {parameters[item.ResourceId].ParameterName}";
            }));
        return $"SELECT {columns} FROM {sourceObject}{where}";
    }

    private static IReadOnlyDictionary<string, SugarParameter> BuildParameters(IReadOnlyList<ApplicationMappingCacheParameter> definitions, IReadOnlyDictionary<string, object?>? supplied)
    {
        supplied ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        var result = new Dictionary<string, SugarParameter>(StringComparer.Ordinal);
        var knownResourceIds = definitions.Select(item => item.ResourceId).ToHashSet(StringComparer.Ordinal);
        foreach (var resourceId in supplied.Keys)
            if (!knownResourceIds.Contains(resourceId))
                throw Invalid($"Mapping Cache test parameter Resource ID is not declared: {resourceId}.");
        foreach (var definition in definitions)
        {
            if (!supplied.TryGetValue(definition.ResourceId, out var value))
            {
                if (definition.Required && definition.DefaultValue is null)
                    throw Invalid($"Required Mapping Cache parameter is missing: {definition.Name} ({definition.ResourceId}).");
                value = definition.DefaultValue;
            }
            var parameterName = ApplicationDataSourceSqlNamePolicy.RequireIdentifier(definition.ResourceId.Replace('-', '_').Replace(':', '_'), "parameter Resource ID");
            result[definition.ResourceId] = new SugarParameter("@" + parameterName, ConvertValue(definition.DataType, value, $"parameters.{definition.ResourceId}"));
        }
        return result;
    }

    private async Task WriteAuditAsync(ApplicationMappingCacheEntity cache, ApplicationDataSourceEntity source, string sql, int timeoutSeconds, long durationMs, int rows, bool success, string? error, bool cancellationRequested, CancellationToken cancellationToken)
    {
        if (auditWriter is null)
            throw Invalid("Mapping cache execution requires an available audit sink.");
        var hash = Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(sql))).ToLowerInvariant();
        await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
            Id = Guid.NewGuid().ToString("N"), TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), SourceKind = "MappingCache", SourceId = cache.Id, SourceName = cache.CacheName, DataSourceId = source.Id, ScriptHash = hash, ScriptPreview = sql[..Math.Min(sql.Length, 1000)], StatementSummary = "SELECT", RiskSummary = "readOnly", Operation = "mapping-cache.execute", ResourceKind = ApplicationDataResourceKind.MappingCache, Outcome = success ? "Succeeded" : "Failed", FailureCode = success ? null : cancellationRequested ? "OperationCanceled" : "MappingCacheExecutionFailed", Provider = source.ObjectType, TimeoutMs = timeoutSeconds * 1000, CancellationRequested = cancellationRequested, RequestHash = hash, RedactedDetailsJson = JsonSerializer.Serialize(new { cacheId = cache.Id, returnedRows = rows }), ParameterSummaryJson = "[]", ReturnedRows = rows, DurationMs = durationMs, IsSuccess = success, ErrorMessage = error
        }, cancellationToken);
    }

    private async Task RequireAuditAsync(CancellationToken cancellationToken)
    {
        if (auditWriter is null)
            throw Invalid("Mapping cache execution requires an available audit sink.");
        await auditWriter.EnsureAvailableAsync(cancellationToken);
    }

    private static async Task<DataTable> ExecuteDataTableAsync(ISqlSugarClient db, string sql, IReadOnlyList<SugarParameter> parameters, CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection ?? throw new InvalidOperationException("The data source does not support asynchronous result reading.");
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

    private static void ValidateProvider(IApplicationDataSourceProvider provider, MappingCacheDefinition definition)
    {
        if (!string.Equals(provider.Type, definition.Source.Provider, StringComparison.OrdinalIgnoreCase) || definition.Columns.Count > provider.Capability.MaxMappingCacheColumns || definition.Parameters.Count > provider.Capability.MaxMappingCacheParameters || (!provider.Capability.SupportsMappingCacheParameters && definition.Parameters.Count > 0))
            throw Invalid("Mapping cache exceeds the resolved provider capability.");
    }

    private async Task<ApplicationMappingCacheResponse> MapAsync(ISqlSugarClient db, ApplicationDataCenterWorkspace workspace, ApplicationMappingCacheEntity entity, CancellationToken cancellationToken)
    {
        var definition = await LoadDefinitionAsync(db, entity, cancellationToken);
        var capability = providerRegistry.Resolve(entity.Provider).Capability;
        return new(entity.Id, entity.CacheKey, entity.CacheName, entity.Status, definition.Source, definition.Columns, definition.Parameters, new(capability.Provider, capability.SupportsStructuredMappingCache, capability.SupportsMappingCacheParameters, capability.MaxMappingCacheColumns, capability.MaxMappingCacheParameters, capability.MaxPreviewRows, capability.SupportLevel, capability.SupportReason), entity.Remark, entity.CreatedTime, entity.UpdatedTime, entity.LastRefreshedAt, entity.LastRowCount, entity.LastValidationStatus, entity.LastValidationMessage);
    }

    private static ISugarQueryable<ApplicationMappingCacheEntity> QueryCaches(ISqlSugarClient db, string dataSourceId) => db.Queryable<ApplicationMappingCacheEntity>().Where(item => item.DataSourceId == dataSourceId && !item.IsDeleted);

    private static async Task<ApplicationDataSourceEntity> RequireDataSourceAsync(ISqlSugarClient db, string id, CancellationToken cancellationToken) => await db.Queryable<ApplicationDataSourceEntity>().Where(item => item.Id == id && !item.IsDeleted).FirstAsync(cancellationToken) ?? throw new NotFoundException("Data source not found in the current workspace.", ErrorCodes.ApplicationDataCenterObjectNotFound);

    private static async Task<ApplicationMappingCacheEntity> RequireCacheAsync(ISqlSugarClient db, string dataSourceId, string id, CancellationToken cancellationToken) => await QueryCaches(db, dataSourceId).Where(item => item.Id == id).FirstAsync(cancellationToken) ?? throw new NotFoundException("Mapping cache not found in the current workspace.", ErrorCodes.ApplicationDataCenterObjectNotFound);

    private static async Task EnsureCodeUniqueAsync(ISqlSugarClient db, string code, string? exceptId, CancellationToken cancellationToken)
    {
        if (await db.Queryable<ApplicationMappingCacheEntity>().Where(item => item.CacheKey == code && item.Id != exceptId && !item.IsDeleted).AnyAsync(cancellationToken))
            throw new ValidationException("Mapping cache key already exists.", ErrorCodes.ApplicationDataCenterDuplicateCode);
    }

    private void EnsurePermission(string permissionCode)
    {
        if (!currentUser.HasAsterErpPermission(permissionCode))
            throw new ValidationException("Permission denied.", ErrorCodes.PermissionDenied);
    }

    private ApplicationQueryPlanResourceResolver ResourceResolver =>
        resourceResolver ?? new ApplicationQueryPlanResourceResolver(databaseAccessor);

    private static object? ConvertValue(string dataType, object? value, string path)
    {
        if (value is null) return null;
        try
        {
            if (value is JsonElement element)
            {
                value = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Number => element.GetDecimal(),
                    JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => element.GetRawText()
                };
                if (value is null) return null;
            }
            return ApplicationMappingCacheParameterType.Normalize(dataType) switch
            {
                ApplicationMappingCacheParameterType.String => Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture),
                ApplicationMappingCacheParameterType.Number => Convert.ToDecimal(value, global::System.Globalization.CultureInfo.InvariantCulture),
                ApplicationMappingCacheParameterType.Boolean => Convert.ToBoolean(value, global::System.Globalization.CultureInfo.InvariantCulture),
                ApplicationMappingCacheParameterType.Date => DateTime.Parse(Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture)!, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind),
                ApplicationMappingCacheParameterType.Json => ConvertJsonValue(value),
                _ => throw Invalid($"Unsupported Mapping Cache parameter type at {path}.")
            };
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException or JsonException)
        {
            throw Invalid($"Mapping Cache parameter value is invalid at {path}.");
        }
    }

    private static string ConvertJsonValue(object value)
    {
        if (value is JsonElement element)
        {
            using var document = JsonDocument.Parse(element.GetRawText());
            return document.RootElement.GetRawText();
        }
        if (value is string text)
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.GetRawText();
        }
        return JsonSerializer.Serialize(value);
    }

    private static object? DeserializeDefault(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<object>(json);

    private static string NormalizeStoredType(string type)
    {
        try { return ApplicationMappingCacheParameterType.Normalize(type); }
        catch (ArgumentException) { throw Invalid($"Unsupported Mapping Cache parameter type: {type}."); }
    }

    private static void RequireType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw Invalid("Mapping cache data type is required.");
    }

    private static string RequireIdentifier(string value, string name) => ApplicationDataSourceSqlNamePolicy.RequireIdentifier(value.Trim(), name);
    private static string? NormalizeRemark(string? value) => ApplicationDataCenterCodePolicy.NormalizeOptional(value, 2000);
    private static ValidationException Invalid(string message) => new(message, ErrorCodes.ApplicationDataCenterInvalidConfig);

    private sealed record MappingCacheDefinition(ApplicationMappingCacheSource Source, ApplicationMappingCacheTarget Target, IReadOnlyList<ApplicationMappingCacheColumn> Columns, IReadOnlyList<ApplicationMappingCacheParameter> Parameters, ApplicationQueryPlanResolvedResource ResolvedSource);
    private sealed record MappingCacheExecution(bool Success, string Message, long DurationMs, IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields, IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);
}
