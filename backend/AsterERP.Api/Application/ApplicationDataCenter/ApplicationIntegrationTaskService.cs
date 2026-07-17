using System.Data;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationIntegrationTaskService(
    IRepository<ApplicationIntegrationTaskEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataSourceProviderRegistry providerRegistry)
    : ApplicationDataCenterObjectService<ApplicationIntegrationTaskEntity>(repository, databaseAccessor, workspaceResolver, secretProtector, riskGuard, referenceService, templateCatalog, snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.IntegrationTask;

    protected override void ApplySpecificFields(ApplicationIntegrationTaskEntity entity, ApplicationDataCenterObjectUpsertRequest request, bool isCreate)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.SourceObjectId = ReadString(config, "sourceObjectId");
        entity.TargetObjectId = ReadString(config, "targetObjectId");
        entity.TriggerMode = ReadString(config, "triggerMode") ?? "Manual";
        entity.IsEnabled = !string.Equals(ReadString(config, "isEnabled"), "false", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task<ApplicationDataCenterActionResultResponse> TestAsync(string id, ApplicationDataCenterActionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await DryRunAsync(id, cancellationToken);
        return new ApplicationDataCenterActionResultResponse(result.Success, result.Success ? ApplicationDataCenterObjectStatus.Normal : ApplicationDataCenterObjectStatus.Error, result.ErrorMessage ?? "集成任务 DryRun 完成", 0, ApplicationDataCenterJson.Serialize(result), TemplateCatalog.BuildNextActions(ModuleKey, id, ApplicationDataCenterObjectStatus.Draft));
    }

    public Task<ApplicationIntegrationTaskRunResponse> DryRunAsync(string id, CancellationToken cancellationToken = default) => ExecuteAsync(id, false, false, cancellationToken);

    public Task<ApplicationIntegrationTaskRunResponse> RunAsync(string id, ApplicationIntegrationTaskRunRequest request, CancellationToken cancellationToken = default) => ExecuteAsync(id, true, request.StopOnError, cancellationToken);

    protected override async Task ValidateForPublishAsync(ApplicationIntegrationTaskEntity entity, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entity.SourceObjectId) || string.IsNullOrWhiteSpace(entity.TargetObjectId))
            throw new ValidationException("集成任务必须配置来源和目标对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var sourceSnapshot = await SnapshotService.GetLatestAsync(ApplicationDataCenterModuleKey.DataSource, entity.SourceObjectId, cancellationToken);
        var targetSnapshot = await SnapshotService.GetLatestAsync(ApplicationDataCenterModuleKey.DataSource, entity.TargetObjectId, cancellationToken);
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var sourceProvider = providerRegistry.Resolve(sourceSnapshot.ObjectType);
        var sourceTable = RequireTable(config, "sourceTable", sourceProvider);
        _ = RequireTable(config, "targetTable", providerRegistry.Resolve(targetSnapshot.ObjectType));
        var mappings = ReadMappings(config);
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var catalogTable = await LoadCatalogTableAsync(db, entity.SourceObjectId, sourceTable, cancellationToken);
        EnsureMappedSourceFields(mappings, catalogTable);
        _ = ResolveOrderFields(config, catalogTable);
    }

    protected override IReadOnlyDictionary<string, object?> BuildSnapshotBinding(ApplicationIntegrationTaskEntity entity) =>
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["sourceObjectId"] = entity.SourceObjectId, ["targetObjectId"] = entity.TargetObjectId, ["configJson"] = entity.ConfigJson };

    private async Task<ApplicationIntegrationTaskRunResponse> ExecuteAsync(string id, bool write, bool stopOnError, CancellationToken cancellationToken)
    {
        var taskSnapshot = await SnapshotService.GetLatestAsync(ModuleKey, id, cancellationToken);
        var binding = ApplicationDataCenterPublishedSnapshotService.ReadBinding(taskSnapshot.BindingJson);
        var sourceId = ReadString(binding, "sourceObjectId") ?? throw new ValidationException("发布快照缺少来源", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var targetId = ReadString(binding, "targetObjectId") ?? throw new ValidationException("发布快照缺少目标", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var config = ApplicationDataCenterJson.DeserializeDictionary(ReadString(binding, "configJson") ?? taskSnapshot.ConfigJson);
        var sourceSnapshot = await SnapshotService.GetLatestAsync(ApplicationDataCenterModuleKey.DataSource, sourceId, cancellationToken);
        var targetSnapshot = await SnapshotService.GetLatestAsync(ApplicationDataCenterModuleKey.DataSource, targetId, cancellationToken);
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var source = await LoadRuntimeSourceAsync(db, sourceId, sourceSnapshot, cancellationToken);
        var target = await LoadRuntimeSourceAsync(db, targetId, targetSnapshot, cancellationToken);
        var mappings = ReadMappings(config);
        var sourceProvider = providerRegistry.Resolve(source.ObjectType);
        var targetProvider = providerRegistry.Resolve(target.ObjectType);
        var sourceTable = RequireTable(config, "sourceTable", sourceProvider);
        var targetTable = RequireTable(config, "targetTable", targetProvider);
        var catalogTable = await LoadCatalogTableAsync(db, sourceId, sourceTable, cancellationToken);
        EnsureMappedSourceFields(mappings, catalogTable);
        var orderFields = ResolveOrderFields(config, catalogTable);
        var pageSize = ResolvePageSize(config, sourceProvider.Capability.MaxPageSize);
        var sourceSelect = string.Join(", ", mappings.Select(item => sourceProvider.QuoteIdentifier(item.Source)));
        var orderBy = " ORDER BY " + string.Join(", ", orderFields.Select(sourceProvider.QuoteIdentifier));
        using var sourceDb = await connectionFactory.CreateDatabaseClientAsync(source, cancellationToken);
        using var targetDb = await connectionFactory.CreateDatabaseClientAsync(target, cancellationToken);
        var failed = new List<IReadOnlyDictionary<string, object?>>();
        var success = 0;
        var readCount = 0;
        var offset = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var rows = await sourceDb.Ado.GetDataTableAsync(
                sourceProvider.BuildPageSql($"SELECT {sourceSelect} FROM {QuoteTable(source.ObjectType, sourceTable)}", orderBy, offset, pageSize),
                cancellationToken);
            readCount += rows.Rows.Count;
            foreach (DataRow row in rows.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = mappings.ToDictionary(item => item.Target, item => ReadSourceValue(row, item.Source), StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (write) await WriteTargetRowAsync(targetDb, target.ObjectType, QuoteTable(target.ObjectType, targetTable), values, config, cancellationToken);
                    success++;
                }
                catch (Exception exception)
                {
                    failed.Add(new Dictionary<string, object?> { ["error"] = exception.Message, ["row"] = values });
                    if (stopOnError) break;
                }
            }

            if (failed.Count > 0 && stopOnError)
            {
                break;
            }

            if (rows.Rows.Count < pageSize)
            {
                break;
            }

            offset = checked(offset + pageSize);
        }
        var firstError = failed.FirstOrDefault();
        var result = new ApplicationIntegrationTaskRunResponse(failed.Count == 0, failed.Count == 0 ? "Success" : "Failed", readCount, success, failed.Count, failed, firstError is null ? null : Convert.ToString(firstError["error"]), null, !write, taskSnapshot.Id, taskSnapshot.VersionNo);
        if (write) await SaveRunAsync(db, id, result, cancellationToken);
        return result;
    }

    private async Task<ApplicationDataSourceEntity> LoadRuntimeSourceAsync(ISqlSugarClient db, string id, ApplicationDataCenterPublishedSnapshot snapshot, CancellationToken cancellationToken)
    {
        var source = await db.Queryable<ApplicationDataSourceEntity>().Where(item => item.Id == id && !item.IsDeleted).FirstAsync(cancellationToken)
            ?? throw new NotFoundException("运行时数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        source.ConfigJson = snapshot.ConfigJson;
        return source;
    }

    private static async Task<ApplicationDataSourceCatalogTableResponse> LoadCatalogTableAsync(
        ISqlSugarClient db,
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken)
    {
        var segments = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var schema = segments.Length == 2 ? segments[0] : null;
        var name = segments[^1];
        var snapshot = await db.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item => item.DataSourceId == dataSourceId)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        var catalog = snapshot is null
            ? []
            : ApplicationDataCenterJson.Deserialize<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>>(snapshot.CatalogJson) ?? [];
        var table = catalog.FirstOrDefault(item =>
            string.Equals(item.TableName, name, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SchemaName ?? string.Empty, schema ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        return table ?? throw new ValidationException(
            $"源表 '{tableName}' 不在最新 catalog snapshot 中，无法安全分页集成。",
            ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static void EnsureMappedSourceFields(
        IReadOnlyList<(string Source, string Target)> mappings,
        ApplicationDataSourceCatalogTableResponse table)
    {
        var fields = table.Columns.Select(item => item.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = mappings.Select(item => item.Source).Where(item => !fields.Contains(item)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (missing.Length > 0)
        {
            throw new ValidationException(
                $"mappings 包含不在 catalog 中的源字段: {string.Join(", ", missing)}",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static IReadOnlyList<string> ResolveOrderFields(
        IReadOnlyDictionary<string, object?> config,
        ApplicationDataSourceCatalogTableResponse table)
    {
        var configured = ReadStringList(config, "orderByFields");
        var fields = table.Columns.Select(item => item.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var orderFields = configured.Count > 0
            ? configured
            : table.Columns.Where(item => item.PrimaryKey).OrderBy(item => item.Order).Select(item => item.ColumnName).ToArray();
        if (orderFields.Count == 0)
        {
            throw new ValidationException(
                "集成任务必须配置 orderByFields，或源表必须存在主键以提供稳定分页顺序。",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var missing = orderFields.Where(item => !fields.Contains(item)).ToArray();
        if (missing.Length > 0)
        {
            throw new ValidationException(
                $"orderByFields 包含不在 catalog 中的字段: {string.Join(", ", missing)}",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return orderFields;
    }

    private static int ResolvePageSize(IReadOnlyDictionary<string, object?> config, int maxPageSize)
    {
        var requested = config.TryGetValue("pageSize", out var raw) && raw is JsonElement element && element.TryGetInt32(out var parsed)
            ? parsed
            : 500;
        return Math.Clamp(requested <= 0 ? 500 : requested, 1, Math.Min(500, maxPageSize));
    }

    private static object? ReadSourceValue(DataRow row, string source)
    {
        var column = row.Table.Columns.Cast<DataColumn>().FirstOrDefault(item => string.Equals(item.ColumnName, source, StringComparison.OrdinalIgnoreCase));
        return column is null || row[column] == DBNull.Value ? null : row[column];
    }

    private static async Task WriteTargetRowAsync(ISqlSugarClient db, string sourceType, string table, IReadOnlyDictionary<string, object?> values, IReadOnlyDictionary<string, object?> config, CancellationToken cancellationToken)
    {
        if (values.Count == 0) throw new ValidationException("字段映射为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var strategy = ReadString(config, "duplicateStrategy")?.ToLowerInvariant() ?? "fail";
        var keys = ReadStringList(config, "keyFields");
        if (strategy is "skip" or "update" && keys.Count == 0) throw new ValidationException("Skip/Update 重复策略必须配置 keyFields", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var parameters = values.Select((item, index) => new SugarParameter($"@v{index}", Normalize(item.Value))).ToArray();
        var names = values.Keys.Select(field => ApplicationDataSourceSqlNamePolicy.Quote(sourceType, field)).ToArray();
        var placeholders = parameters.Select(item => item.ParameterName).ToArray();
        try
        {
            await db.Ado.ExecuteCommandAsync($"INSERT INTO {table} ({string.Join(",", names)}) VALUES ({string.Join(",", placeholders)})", parameters, cancellationToken);
        }
        catch (Exception exception) when ((strategy is "skip" or "update") && ApplicationIntegrationDuplicateViolationClassifier.IsUniqueConstraintViolation(sourceType, exception))
        {
            if (strategy == "skip") return;
            var update = values.Where(item => !keys.Contains(item.Key, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (update.Length == 0) return;
            var updateParams = new List<SugarParameter>();
            var set = update.Select((item, index) => { var p = new SugarParameter($"@u{index}", Normalize(item.Value)); updateParams.Add(p); return $"{ApplicationDataSourceSqlNamePolicy.Quote(sourceType, item.Key)}={p.ParameterName}"; }).ToArray();
            var where = keys.Select((key, index) => { var p = new SugarParameter($"@k{index}", Normalize(values[key])); updateParams.Add(p); return $"{ApplicationDataSourceSqlNamePolicy.Quote(sourceType, key)}={p.ParameterName}"; }).ToArray();
            await db.Ado.ExecuteCommandAsync($"UPDATE {table} SET {string.Join(",", set)} WHERE {string.Join(" AND ", where)}", updateParams.ToArray(), cancellationToken);
        }
    }

    private async Task SaveRunAsync(ISqlSugarClient db, string taskId, ApplicationIntegrationTaskRunResponse result, CancellationToken cancellationToken)
    {
        var workspace = WorkspaceResolver.Resolve();
        var run = new ApplicationIntegrationTaskRunEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, TaskId = taskId, TriggerType = "Manual", Result = result.Result, StartedAt = DateTime.UtcNow, FinishedAt = DateTime.UtcNow, ReadCount = result.ReadCount, SuccessCount = result.SuccessCount, FailedCount = result.FailedCount, ErrorMessage = result.ErrorMessage, OutputJson = ApplicationDataCenterJson.Serialize(result), CreatedBy = workspace.UserId, UpdatedBy = workspace.UserId, UpdatedTime = DateTime.UtcNow };
        await db.Insertable(run).ExecuteCommandAsync(cancellationToken);
    }

    private static IReadOnlyList<(string Source, string Target)> ReadMappings(IReadOnlyDictionary<string, object?> config)
    {
        if (!config.TryGetValue("mappings", out var raw) || raw is not JsonElement element || element.ValueKind != JsonValueKind.Array)
            throw new ValidationException("集成任务必须配置 mappings", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var result = element.EnumerateArray().Select(item => (Source: item.TryGetProperty("sourceField", out var source) ? source.GetString() : null, Target: item.TryGetProperty("targetField", out var target) ? target.GetString() : null)).Where(item => !string.IsNullOrWhiteSpace(item.Source) && !string.IsNullOrWhiteSpace(item.Target)).Select(item => (item.Source!, item.Target!)).ToArray();
        return result.Length == 0 ? throw new ValidationException("字段映射为空", ErrorCodes.ApplicationDataCenterInvalidConfig) : result;
    }

    private static string RequireTable(IReadOnlyDictionary<string, object?> config, string key, IApplicationDataSourceProvider provider)
    {
        var value = ReadString(config, key)?.Trim();
        var segments = value?.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
        if (segments.Length is < 1 or > 2 || segments.Any(segment => !global::System.Text.RegularExpressions.Regex.IsMatch(segment, "^[A-Za-z_][A-Za-z0-9_]*$")))
            throw new ValidationException($"{key} must be a valid table name", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (segments.Length == 2 && !provider.Capability.SupportsSchemas)
            throw new ValidationException($"Provider '{provider.Type}' does not support schema-qualified table access.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (string.IsNullOrWhiteSpace(value) || !global::System.Text.RegularExpressions.Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_.]*$")) throw new ValidationException($"{key} 不合法", ErrorCodes.ApplicationDataCenterInvalidConfig);
        return string.Join('.', segments);
    }

    private static string QuoteTable(string provider, string table)
    {
        var segments = table.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ApplicationDataSourceSqlNamePolicy.QuoteQualified(
            provider,
            segments.Length == 2 ? segments[0] : null,
            segments.Length == 2 ? segments[1] : segments[0]);
    }

    private static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, object?> config, string key) => config.TryGetValue(key, out var raw) && raw is JsonElement element && element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray() : [];
    private static object? Normalize(object? value) => value is DBNull ? null : value is JsonElement element ? element.ToString() : value;
    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) => config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;
}
