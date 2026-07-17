using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableWorkbenchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataSourceService dataSourceService,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataCenterSqlScriptAuditWriter auditWriter,
    ApplicationDataSourceProviderRegistry providerRegistry)
{
    public Task<IReadOnlyList<ApplicationDataSourceTableResponse>> GetTablesAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default) =>
        dataSourceService.GetTablesAsync(dataSourceId, cancellationToken);

    public async Task<ApplicationDataSourceSchemaChangePlanResponse> CreateAlterTablePlanAsync(
        string dataSourceId,
        ApplicationDataSourceAlterTableRequest request,
        CancellationToken cancellationToken = default)
    {
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, dataSourceId, cancellationToken);
        EnsureWritableDatabase(dataSource);
        var tableName = ApplicationDataSourceSqlNamePolicy.RequireIdentifier(request.TableName, "表名");
        var schemaName = ApplicationDataSourceSqlNamePolicy.OptionalIdentifier(request.SchemaName, "Schema");
        var provider = ResolveProvider(dataSource.ObjectType);
        EnsureSchemaCapability(provider, schemaName);
        var detail = await GetTableAsync(dataSourceId, BuildQualifiedName(schemaName, tableName), cancellationToken);
        if (detail.Table.TableType.Contains("VIEW", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("视图不能通过表结构计划修改。", ErrorCodes.ApplicationDataCenterInvalidConfig);

        var currentColumns = detail.Columns.OrderBy(item => item.Order)
            .Select(item => new ApplicationDataSourceCreateTableColumnRequest(item.ColumnName, item.DataType, item.PrimaryKey ? false : item.Nullable, item.PrimaryKey, null, null)).ToArray();
        var desiredColumns = NormalizeColumns(request.Columns);
        var statements = provider.BuildAlterTableSql(schemaName, tableName, currentColumns, desiredColumns);
        var target = BuildQualifiedName(schemaName, tableName);
        var dependencySnapshot = await ReadSchemaDependenciesAsync(appDb, dataSourceId, schemaName, tableName, currentColumns, cancellationToken);
        var estimatedAffectedRows = await TryEstimateAffectedRowsAsync(dataSource, provider, schemaName, tableName, cancellationToken);
        var destructive = currentColumns.Any(item => desiredColumns.All(candidate => !string.Equals(candidate.ColumnName, item.ColumnName, StringComparison.OrdinalIgnoreCase))) ||
                          currentColumns.Any(item => desiredColumns.Any(candidate => string.Equals(candidate.ColumnName, item.ColumnName, StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(candidate.DataType, item.DataType, StringComparison.OrdinalIgnoreCase)));
        var nonNullableAdds = desiredColumns.Any(item => currentColumns.All(candidate => !string.Equals(candidate.ColumnName, item.ColumnName, StringComparison.OrdinalIgnoreCase)) &&
            !item.Nullable && string.IsNullOrWhiteSpace(item.DefaultValue));
        var risks = new List<string>
        {
            "修改表结构可能持有元数据锁，部署期间相关对象可能短暂不可用。",
            $"本计划包含 {statements.Count} 条 provider SQL，部署前会重新读取当前结构并校验 hash。"
        };
        if (destructive)
            risks.Add("删除字段或改变字段类型可能造成数据丢失或截断，必须确认并准备外部备份。");
        if (nonNullableAdds && estimatedAffectedRows.GetValueOrDefault() > 0)
            risks.Add("向已有数据表新增无默认值的非空字段可能因现有行无法回填而失败，必须先安排回填或停机窗口。");
        if (!estimatedAffectedRows.HasValue)
            risks.Add("当前 provider 无法在预览阶段可靠估算影响行数，部署前必须完成外部备份和人工确认。");

        if (!dependencySnapshot.IsAvailable)
            risks.Add("Catalog Snapshot 不可用，无法完整核对约束、索引和触发器依赖，部署前必须刷新预览。");

        var planHash = ComputeAlterPlanHash(dataSourceId, dataSource.ObjectType, target, currentColumns, desiredColumns, statements, dependencySnapshot.Dependencies);
        var plan = new ApplicationDataSourceSchemaChangePlanResponse(
            $"plan_{planHash[..24]}", planHash, dataSourceId, dataSource.ObjectType, "AlterTable", target,
            string.Join(Environment.NewLine, statements), risks, destructive || (nonNullableAdds && estimatedAffectedRows.GetValueOrDefault() > 0) ? "high" : "medium", true, estimatedAffectedRows,
            dependencySnapshot.Dependencies, true,
            provider.Capability.SupportsTransactionalDdl, DateTime.UtcNow);
        plan = plan with { BeforeColumns = currentColumns, AfterColumns = desiredColumns };
        await PersistPlanAsync(plan, cancellationToken);
        return plan;
    }

    public async Task<ApplicationDataSourceTableDetailResponse> DeployAlterTablePlanAsync(
        string dataSourceId,
        ApplicationDataSourceAlterTablePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed)
            throw new ValidationException("表结构变更必须明确确认后才能部署。", ErrorCodes.PermissionDenied);

        var plan = await GetPersistedPlanAsync(dataSourceId, request.PlanHash, cancellationToken)
            ?? await CreateAlterTablePlanAsync(dataSourceId, request.Table, cancellationToken);
        if (!string.Equals(plan.PlanHash, request.PlanHash, StringComparison.OrdinalIgnoreCase) || !string.Equals(plan.Operation, "AlterTable", StringComparison.Ordinal))
            throw new ValidationException("SchemaChangePlan 已变化，请重新预览并确认", ErrorCodes.ApplicationDataCenterInvalidConfig);

        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, dataSourceId, cancellationToken);
        var provider = ResolveProvider(dataSource.ObjectType);
        var detail = await GetTableAsync(dataSourceId, plan.Target, cancellationToken);
        var currentColumns = detail.Columns.OrderBy(item => item.Order)
            .Select(item => new ApplicationDataSourceCreateTableColumnRequest(item.ColumnName, item.DataType, item.Nullable, item.PrimaryKey, null, null)).ToArray();
        var desiredColumns = NormalizeColumns(request.Table.Columns);
        var qualified = ParseQualifiedName(plan.Target);
        var statements = provider.BuildAlterTableSql(qualified.SchemaName, qualified.TableName, currentColumns, desiredColumns);
        var dependencySnapshot = await ReadSchemaDependenciesAsync(appDb, dataSourceId, qualified.SchemaName, qualified.TableName, currentColumns, cancellationToken);
        if (!string.Equals(ComputeAlterPlanHash(dataSourceId, dataSource.ObjectType, plan.Target, currentColumns, desiredColumns, statements, dependencySnapshot.Dependencies), request.PlanHash, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("SchemaChangePlan 已变化，请重新预览并确认", ErrorCodes.ApplicationDataCenterInvalidConfig);

        await auditWriter.EnsureAvailableAsync(cancellationToken);
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        try
        {
            if (provider.Capability.SupportsTransactionalDdl)
                await db.Ado.BeginTranAsync();
            foreach (var statement in statements)
                await db.Ado.ExecuteCommandAsync(statement, Array.Empty<SugarParameter>(), cancellationToken);
            if (provider.Capability.SupportsTransactionalDdl)
                await db.Ado.CommitTranAsync();
            await UpdatePlanStatusAsync(plan.PlanId, "Applied", cancellationToken);
            await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
            {
                TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), SourceKind = "SchemaChangePlan", SourceId = plan.PlanId,
                SourceName = plan.Target, DataSourceId = dataSourceId, ScriptHash = plan.PlanHash, ScriptPreview = plan.SqlPreview,
                StatementSummary = plan.Operation, RiskSummary = string.Join(";", plan.Risks), IsSuccess = true, Operation = "schema.apply",
                ResourceKind = "table.schema", Outcome = "Succeeded", Provider = dataSource.ObjectType, TimeoutMs = 30_000,
                RequestHash = plan.PlanHash, RedactedDetailsJson = BuildPlanAuditDetails(plan, "Applied")
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            try
            {
                if (provider.Capability.SupportsTransactionalDdl)
                    await db.Ado.RollbackTranAsync();
            }
            catch
            {
                // ManualRecovery below is the fail-closed state when rollback cannot be confirmed.
            }
            await UpdatePlanStatusAsync(plan.PlanId, "ManualRecovery", CancellationToken.None);
            await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
            {
                TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), SourceKind = "SchemaChangePlan", SourceId = plan.PlanId,
                SourceName = plan.Target, DataSourceId = dataSourceId, ScriptHash = plan.PlanHash, ScriptPreview = plan.SqlPreview,
                StatementSummary = plan.Operation, RiskSummary = string.Join(";", plan.Risks), IsSuccess = false, ErrorMessage = exception.Message,
                Operation = "schema.apply", ResourceKind = "table.schema", Outcome = "ManualRecovery", FailureCode = "ExternalDdlOutcomeUnknown",
                Provider = dataSource.ObjectType, TimeoutMs = 30_000, RequestHash = plan.PlanHash,
                RedactedDetailsJson = BuildPlanAuditDetails(plan, "ManualRecovery")
            }, CancellationToken.None);
            throw;
        }

        return await GetTableAsync(dataSourceId, plan.Target, cancellationToken);
    }

    public async Task<ApplicationDataSourceTableDetailResponse> GetTableAsync(
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        var tables = await dataSourceService.GetTablesAsync(dataSourceId, cancellationToken);
        var table = tables.FirstOrDefault(item =>
            string.Equals(BuildQualifiedName(item.SchemaName, item.TableName), tableName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.TableName, tableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("数据表不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        var columns = await dataSourceService.GetColumnsAsync(
            dataSourceId,
            BuildQualifiedName(table.SchemaName, table.TableName),
            cancellationToken);
        return new ApplicationDataSourceTableDetailResponse(table, columns);
    }

    public async Task<ApplicationDataSourceSchemaChangePlanResponse> CreateTablePlanAsync(
        string dataSourceId,
        ApplicationDataSourceCreateTableRequest request,
        CancellationToken cancellationToken = default)
    {
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, dataSourceId, cancellationToken);
        EnsureWritableDatabase(dataSource);

        var tableName = ApplicationDataSourceSqlNamePolicy.RequireIdentifier(request.TableName, "表名");
        var schemaName = ApplicationDataSourceSqlNamePolicy.OptionalIdentifier(request.SchemaName, "Schema");
        var columns = NormalizeColumns(request.Columns);
        var provider = ResolveProvider(dataSource.ObjectType);
        EnsureSchemaCapability(provider, schemaName);
        var sql = provider.BuildCreateTableSql(schemaName, tableName, columns);
        var createdAt = DateTime.UtcNow;
        var plan = new
        {
            dataSourceId,
            provider = dataSource.ObjectType,
            operation = "CreateTable",
            target = BuildQualifiedName(schemaName, tableName),
            sql,
            columns
        };
        var planJson = global::System.Text.Json.JsonSerializer.Serialize(plan);
        var planHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planJson))).ToLowerInvariant();
        var planId = $"plan_{planHash[..24]}";
        var reversible = provider.Capability.SupportsTransactionalDdl;
        var risks = new List<string> { "将创建新的数据库对象", "部署期间可能持有元数据锁" };
        if (!reversible)
        {
            risks.Add("current provider does not support transactional DDL; rollback is unavailable and failure or timeout requires ManualRecovery with external object verification.");
        }

        var response = new ApplicationDataSourceSchemaChangePlanResponse(planId, planHash, dataSourceId, dataSource.ObjectType, "CreateTable", BuildQualifiedName(schemaName, tableName), sql, risks, "medium", true, null, [], true, reversible, createdAt)
        {
            AfterColumns = columns
        };
        await PersistPlanAsync(response, cancellationToken);
        return response;
    }

    public async Task<ApplicationDataSourceTableDetailResponse> DeployTablePlanAsync(
        string dataSourceId,
        ApplicationDataSourceSchemaChangePlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.Confirmed)
        {
            throw new ValidationException("高风险数据库变更必须明确确认后才能部署", ErrorCodes.PermissionDenied);
        }

        var plan = await GetPersistedPlanAsync(dataSourceId, request.PlanHash, cancellationToken)
            ?? await CreateTablePlanAsync(dataSourceId, request.Table, cancellationToken);
        if (!string.Equals(plan.PlanHash, request.PlanHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("SchemaChangePlan 已变化，请重新预览并确认", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, dataSourceId, cancellationToken);
        var provider = ResolveProvider(dataSource.ObjectType);
        await auditWriter.EnsureAvailableAsync(cancellationToken);
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var started = Stopwatch.GetTimestamp();
        var transactionStarted = false;
        var commandStarted = false;
        var commandSucceeded = false;
        var commitAttempted = false;
        var commitSucceeded = false;
        try
        {
            if (provider.Capability.SupportsTransactionalDdl)
            {
                await db.Ado.BeginTranAsync();
                transactionStarted = true;
            }

            commandStarted = true;
            await db.Ado.ExecuteCommandAsync(plan.SqlPreview, Array.Empty<SugarParameter>(), cancellationToken);
            commandSucceeded = true;
            if (transactionStarted)
            {
                commitAttempted = true;
                await db.Ado.CommitTranAsync();
                commitSucceeded = true;
            }

            await UpdatePlanStatusAsync(plan.PlanId, "Applied", cancellationToken);
            await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
            {
                TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
                SourceKind = "SchemaChangePlan",
                SourceId = plan.PlanId,
                SourceName = plan.Target,
                DataSourceId = dataSourceId,
                ScriptHash = plan.PlanHash,
                ScriptPreview = plan.SqlPreview,
                StatementSummary = plan.Operation,
                RiskSummary = string.Join(";", plan.Risks),
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                IsSuccess = true,
                Operation = "schema.apply",
                ResourceKind = "table.schema",
                Outcome = "Succeeded",
                Provider = dataSource.ObjectType,
                TimeoutMs = 30_000,
                RequestHash = plan.PlanHash,
                RedactedDetailsJson = BuildPlanAuditDetails(plan, "Applied")
            }, CancellationToken.None);
        }
        catch (Exception exception)
        {
            var rollbackSucceeded = false;
            if (transactionStarted && !commitAttempted)
            {
                try
                {
                    await db.Ado.RollbackTranAsync();
                    rollbackSucceeded = true;
                }
                catch
                {
                    rollbackSucceeded = false;
                }
            }

            var externalOutcomeUnknown = (commandStarted && !commandSucceeded && (!transactionStarted || !rollbackSucceeded)) || (commitAttempted && !commitSucceeded);
            var manualRecovery = externalOutcomeUnknown || (commandSucceeded && (!transactionStarted || commitSucceeded));
            var planStatus = manualRecovery
                ? "ManualRecovery"
                : exception is OperationCanceledException ? "Canceled" : rollbackSucceeded ? "Failed" : "ManualRecovery";
            var auditOutcome = manualRecovery ? "ManualRecovery" : exception is OperationCanceledException ? "Canceled" : "Failed";
            var failureCode = manualRecovery
                ? "ExternalDdlOutcomeUnknown"
                : exception is BusinessException businessException ? businessException.Code.ToString() : exception.GetType().Name;
            await UpdatePlanStatusAsync(plan.PlanId, planStatus, CancellationToken.None);
            await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
            {
                TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
                SourceKind = "SchemaChangePlan",
                SourceId = plan.PlanId,
                SourceName = plan.Target,
                DataSourceId = dataSourceId,
                ScriptHash = plan.PlanHash,
                ScriptPreview = plan.SqlPreview,
                StatementSummary = plan.Operation,
                RiskSummary = string.Join(";", plan.Risks),
                DurationMs = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                IsSuccess = false,
                ErrorMessage = exception.Message,
                Operation = "schema.apply",
                ResourceKind = "table.schema",
                Outcome = auditOutcome,
                FailureCode = failureCode,
                CancellationRequested = exception is OperationCanceledException,
                Provider = dataSource.ObjectType,
                TimeoutMs = 30_000,
                RequestHash = plan.PlanHash,
                RedactedDetailsJson = BuildPlanAuditDetails(plan, planStatus)
            }, CancellationToken.None);
            throw;
        }

        return await GetTableAsync(dataSourceId, plan.Target, cancellationToken);
    }

    private async Task UpdatePlanStatusAsync(string planId, string status, CancellationToken cancellationToken)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await db.Updateable<ApplicationDataSourceSchemaChangePlanEntity>()
            .SetColumns(item => item.Status == status)
            .Where(item => item.Id == planId)
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task PersistPlanAsync(ApplicationDataSourceSchemaChangePlanResponse plan, CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var exists = await db.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .Where(item => item.PlanHash == plan.PlanHash && item.DataSourceId == plan.DataSourceId)
            .AnyAsync(cancellationToken);
        if (exists)
            return;

        await db.Insertable(new ApplicationDataSourceSchemaChangePlanEntity
        {
            Id = plan.PlanId,
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            DataSourceId = plan.DataSourceId,
            Provider = plan.Provider,
            Operation = plan.Operation,
            Target = plan.Target,
            SqlPreview = plan.SqlPreview,
            RisksJson = ApplicationDataCenterJson.Serialize(plan.Risks),
            RiskLevel = plan.RiskLevel,
            RequiresLock = plan.RequiresLock,
            EstimatedAffectedRows = plan.EstimatedAffectedRows,
            EstimatedAffectedRowsStatus = plan.EstimatedAffectedRows.HasValue ? "Known" : "Unknown",
            DependenciesJson = ApplicationDataCenterJson.Serialize(plan.Dependencies),
            BeforeColumnsJson = ApplicationDataCenterJson.Serialize(plan.BeforeColumns),
            AfterColumnsJson = ApplicationDataCenterJson.Serialize(plan.AfterColumns),
            RequiresConfirmation = plan.RequiresConfirmation,
            Reversible = plan.Reversible,
            PlanHash = plan.PlanHash,
            PlannedAt = plan.CreatedAt,
            Status = "Planned"
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static string BuildPlanAuditDetails(ApplicationDataSourceSchemaChangePlanResponse plan, string deploymentStatus = "Planned")
    {
        var status = plan.EstimatedAffectedRows.HasValue ? "known" : "unknown";
        var value = plan.EstimatedAffectedRows?.ToString(global::System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        return $"{{\"planId\":\"{plan.PlanId}\",\"riskCount\":{plan.Risks.Count},\"estimatedAffectedRows\":{value},\"estimatedAffectedRowsStatus\":\"{status}\",\"deploymentStatus\":\"{deploymentStatus}\",\"reversible\":{plan.Reversible.ToString().ToLowerInvariant()}}}";
    }

    private static string ComputeAlterPlanHash(
        string dataSourceId,
        string provider,
        string target,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> currentColumns,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> desiredColumns,
        IReadOnlyList<string> statements,
        IReadOnlyList<string> dependencies)
    {
        var planJson = global::System.Text.Json.JsonSerializer.Serialize(new
        {
            dataSourceId,
            provider,
            operation = "AlterTable",
            target,
            currentColumns,
            desiredColumns,
            statements,
            dependencies
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(planJson))).ToLowerInvariant();
    }

    private static async Task<SchemaDependencySnapshot> ReadSchemaDependenciesAsync(
        ISqlSugarClient appDb,
        string dataSourceId,
        string? schemaName,
        string tableName,
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> currentColumns,
        CancellationToken cancellationToken)
    {
        var snapshots = await appDb.Queryable<ApplicationDataSourceCatalogSnapshotEntity>()
            .Where(item => item.DataSourceId == dataSourceId && !item.IsDeleted)
            .OrderByDescending(item => item.VersionNo)
            .Take(1)
            .ToListAsync(cancellationToken);
        var dependencies = new List<string>(currentColumns
            .Where(item => item.PrimaryKey)
            .Select(item => $"primary-key:{item.ColumnName}"));
        var snapshot = snapshots.FirstOrDefault();
        if (snapshot is null)
            return new(false, dependencies);

        var catalog = ApplicationDataCenterJson.Deserialize<IReadOnlyList<ApplicationDataSourceCatalogTableResponse>>(snapshot.CatalogJson) ?? [];
        var table = catalog.FirstOrDefault(item =>
            string.Equals(item.TableName, tableName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SchemaName ?? string.Empty, schemaName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        if (table is null)
            return new(false, dependencies);

        dependencies.AddRange(table.Constraints.Select(item => $"constraint:{item.Name}"));
        dependencies.AddRange(table.Indexes.Select(item => $"index:{item.Name}"));
        dependencies.AddRange(table.Triggers.Select(item => $"trigger:{item.Name}"));
        return new(true, dependencies.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private sealed record SchemaDependencySnapshot(bool IsAvailable, IReadOnlyList<string> Dependencies);

    private async Task<int?> TryEstimateAffectedRowsAsync(
        ApplicationDataSourceEntity dataSource,
        IApplicationDataSourceProvider provider,
        string? schemaName,
        string tableName,
        CancellationToken cancellationToken)
    {
        try
        {
            using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
            var quotedTable = provider.QuoteQualified(schemaName, tableName);
            var countSql = provider.BuildCountSql(quotedTable, string.Empty);
            var value = await ExecuteScalarAsync(db, countSql, cancellationToken);
            return value is null || value is DBNull ? null : Convert.ToInt32(value, global::System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<object?> ExecuteScalarAsync(
        ISqlSugarClient db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步标量读取");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private async Task<ApplicationDataSourceSchemaChangePlanResponse?> GetPersistedPlanAsync(string dataSourceId, string planHash, CancellationToken cancellationToken)
    {
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = (await db.Queryable<ApplicationDataSourceSchemaChangePlanEntity>()
            .Where(item => item.DataSourceId == dataSourceId && item.PlanHash == planHash && item.Status == "Planned")
            .OrderBy(item => item.PlannedAt, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return entity is null
            ? null
            : new ApplicationDataSourceSchemaChangePlanResponse(
                entity.Id,
                entity.PlanHash,
                entity.DataSourceId,
                entity.Provider,
                entity.Operation,
                entity.Target,
                entity.SqlPreview,
                ApplicationDataCenterJson.Deserialize<IReadOnlyList<string>>(entity.RisksJson) ?? [],
                entity.RiskLevel,
                entity.RequiresLock,
                entity.EstimatedAffectedRows,
                ApplicationDataCenterJson.Deserialize<IReadOnlyList<string>>(entity.DependenciesJson) ?? [],
                entity.RequiresConfirmation,
                entity.Reversible,
                entity.PlannedAt)
            {
                BeforeColumns = ApplicationDataCenterJson.Deserialize<IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest>>(entity.BeforeColumnsJson) ?? [],
                AfterColumns = ApplicationDataCenterJson.Deserialize<IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest>>(entity.AfterColumnsJson) ?? []
            };
    }

    public async Task<ApplicationDataCenterPreviewResponse> PreviewTableAsync(
        string dataSourceId,
        string tableName,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, dataSourceId, cancellationToken);
        EnsureDatabase(dataSource);
        var (schemaName, normalizedTableName) = ParseQualifiedName(tableName);
        var provider = ResolveProvider(dataSource.ObjectType);
        EnsureSchemaCapability(provider, schemaName);
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        var quoted = ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, schemaName, normalizedTableName);
        cancellationToken.ThrowIfCancellationRequested();
        var dataTable = await ExecuteDataTableAsync(
            db,
            provider.BuildPreviewSql($"SELECT * FROM {quoted}", Math.Clamp(maxRows, 1, provider.Capability.MaxPreviewRows)),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return ApplicationDataSourcePreviewMapper.Map(dataTable, "表数据预览成功");
    }

    private static async Task<DataTable> ExecuteDataTableAsync(
        ISqlSugarClient db,
        string sql,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步结果读取");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
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

    private IApplicationDataSourceProvider ResolveProvider(string sourceType) => providerRegistry.Resolve(sourceType);

    private static IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> NormalizeColumns(
        IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> columns)
    {
        if (columns is null || columns.Count == 0)
        {
            throw new ValidationException("至少需要配置一个字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var normalized = columns.Select(item =>
        {
            var name = ApplicationDataSourceSqlNamePolicy.RequireIdentifier(item.ColumnName, "字段名");
            return item with { ColumnName = name, Nullable = item.PrimaryKey ? false : item.Nullable };
        }).ToArray();

        foreach (var column in normalized)
        {
            if (column is null)
                throw new ValidationException("字段定义不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            if (column.ColumnName.Length > 128)
                throw new ValidationException("字段名长度不能超过 128 个字符", ErrorCodes.ApplicationDataCenterInvalidConfig);
            if (string.IsNullOrWhiteSpace(column.DataType) || column.DataType.Length > 64)
                throw new ValidationException("字段类型不能为空且长度不能超过 64 个字符", ErrorCodes.ApplicationDataCenterInvalidConfig);
            if (column.PrimaryKey && column.Nullable)
                throw new ValidationException("主键字段不能允许为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            if (column.DefaultValue?.Length > 512)
                throw new ValidationException("字段默认值表达式长度不能超过 512 个字符", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (normalized.GroupBy(item => item.ColumnName, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new ValidationException("字段名不能重复", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    private static void EnsureWritableDatabase(ApplicationDataSourceEntity dataSource)
    {
        EnsureDatabase(dataSource);
        if (dataSource.IsReadOnly)
        {
            throw new ValidationException("当前数据源为只读，不能创建表", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void EnsureDatabase(ApplicationDataSourceEntity dataSource)
    {
        if (!ApplicationDataSourceConnectionFactory.IsDatabaseType(dataSource.ObjectType))
        {
            throw new ValidationException("当前数据源不是数据库", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void EnsureSchemaCapability(IApplicationDataSourceProvider provider, string? schemaName)
    {
        if (!string.IsNullOrWhiteSpace(schemaName) && !provider.Capability.SupportsSchemas)
        {
            throw new ValidationException(
                $"Provider {provider.Type} does not support schema-qualified data objects.",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static async Task<ApplicationDataSourceEntity> EnsureDataSourceAsync(
        ISqlSugarClient db,
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        return await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == dataSourceId &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private static string BuildQualifiedName(string? schemaName, string tableName) =>
        string.IsNullOrWhiteSpace(schemaName) ? tableName : $"{schemaName}.{tableName}";

    private static (string? SchemaName, string TableName) ParseQualifiedName(string tableName)
    {
        var parts = tableName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2)
        {
            throw new ValidationException("表名不合法", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return parts.Length == 1
            ? (null, ApplicationDataSourceSqlNamePolicy.RequireIdentifier(parts[0], "表名"))
            : (ApplicationDataSourceSqlNamePolicy.RequireIdentifier(parts[0], "Schema"), ApplicationDataSourceSqlNamePolicy.RequireIdentifier(parts[1], "表名"));
    }
}
