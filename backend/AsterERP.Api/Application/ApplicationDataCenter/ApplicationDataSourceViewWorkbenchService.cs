using AsterERP.Api.Infrastructure.Database;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceViewWorkbenchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataPreviewReader previewReader,
    ApplicationDataSourceProviderRegistry providerRegistry,
    ApplicationDataCenterSqlScriptAuditWriter auditWriter)
{
    public async Task<IReadOnlyList<ApplicationDataSourceViewResponse>> GetListAsync(
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var db = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        await EnsureDataSourceAsync(db, workspace, dataSourceId, cancellationToken);
        var items = await db.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item =>
                item.SourceObjectId == dataSourceId &&
                item.IsPhysicalView &&
                !item.IsDeleted)
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToArray();
    }

    public async Task<ApplicationDataSourceViewResponse> CreateAsync(
        string dataSourceId,
        ApplicationDataSourceViewUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, workspace, dataSourceId, cancellationToken);
        EnsureWritableDatabase(dataSource);

        var viewName = ApplicationDataSourceSqlNamePolicy.RequireIdentifier(request.ViewName, "视图名");
        var schemaName = ApplicationDataSourceSqlNamePolicy.OptionalIdentifier(request.SchemaName, "Schema");
        EnsureSchemaCapability(providerRegistry.Resolve(dataSource.ObjectType), schemaName);
        var sql = ApplicationDataSourceSqlPolicy.RequireSelectSql(request.Sql);
        await EnsureUniqueViewCodeAsync(appDb, workspace, dataSourceId, viewName, null, cancellationToken);
        await auditWriter.EnsureAvailableAsync(cancellationToken);
        var externalStarted = false;
        try
        {
            externalStarted = true;
            await CreatePhysicalViewAsync(dataSource, schemaName, viewName, sql, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteViewAuditAsync(
                dataSource,
                dataSourceId,
                "CREATE",
                viewName,
                sql,
                false,
                exception,
                CancellationToken.None,
                externalStarted ? "ManualRecovery" : null,
                externalStarted ? "ExternalDdlUnknown" : null);
            throw;
        }

        var entity = new ApplicationQueryDatasetEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ModuleKey = ApplicationDataCenterModuleKey.QueryDataset,
            ObjectCode = ApplicationDataCenterCodePolicy.NormalizeCode(viewName, "视图编码"),
            ObjectName = ApplicationDataCenterCodePolicy.NormalizeName(request.Alias, "视图别名"),
            ObjectType = ApplicationQueryDatasetType.QueryView,
            Status = ApplicationDataCenterObjectStatus.Published,
            SourceObjectId = dataSourceId,
            RuntimeViewCode = viewName,
            IsPhysicalView = true,
            ViewSchemaName = schemaName,
            ViewSql = sql,
            ConfigJson = BuildConfig(dataSourceId, schemaName, viewName, sql),
            Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000),
            CreatedBy = workspace.UserId,
            CreatedTime = DateTime.UtcNow
        };
        try
        {
            await appDb.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteViewAuditAsync(dataSource, entity.Id, "CREATE", viewName, sql, true, null, CancellationToken.None);
        }
        catch (Exception exception)
        {
            var recoveryFailure = await PersistViewRecoveryAsync(dataSource, entity.Id, "CREATE", viewName, sql, exception);
            if (recoveryFailure is not null)
                throw recoveryFailure;

            throw;
        }
        return Map(entity);
    }

    public async Task<ApplicationDataSourceViewResponse> UpdateAsync(
        string dataSourceId,
        string viewId,
        ApplicationDataSourceViewUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, workspace, dataSourceId, cancellationToken);
        EnsureWritableDatabase(dataSource);
        var entity = await EnsureViewAsync(appDb, workspace, dataSourceId, viewId, cancellationToken);

        var oldConfig = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var oldViewName = ReadString(oldConfig, "viewName") ?? entity.ObjectCode;
        var oldSchemaName = ReadString(oldConfig, "schemaName");
        var viewName = ApplicationDataSourceSqlNamePolicy.RequireIdentifier(request.ViewName, "视图名");
        var schemaName = ApplicationDataSourceSqlNamePolicy.OptionalIdentifier(request.SchemaName, "Schema");
        EnsureSchemaCapability(providerRegistry.Resolve(dataSource.ObjectType), schemaName);
        var sql = ApplicationDataSourceSqlPolicy.RequireSelectSql(request.Sql);
        await EnsureUniqueViewCodeAsync(appDb, workspace, dataSourceId, viewName, entity.Id, cancellationToken);

        await auditWriter.EnsureAvailableAsync(cancellationToken);
        var oldSql = entity.ViewSql ?? ReadString(oldConfig, "sql");
        var externalStarted = false;
        try
        {
            externalStarted = true;
            await ReplacePhysicalViewAsync(dataSource, oldSchemaName, oldViewName, oldSql, schemaName, viewName, sql, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteViewAuditAsync(
                dataSource,
                entity.Id,
                "UPDATE",
                viewName,
                sql,
                false,
                exception,
                CancellationToken.None,
                externalStarted ? "ManualRecovery" : null,
                externalStarted ? "ExternalDdlUnknown" : null);
            throw;
        }

        entity.ObjectCode = ApplicationDataCenterCodePolicy.NormalizeCode(viewName, "视图编码");
        entity.ObjectName = ApplicationDataCenterCodePolicy.NormalizeName(request.Alias, "视图别名");
        entity.SourceObjectId = dataSourceId;
        entity.RuntimeViewCode = viewName;
        entity.IsPhysicalView = true;
        entity.ViewSchemaName = schemaName;
        entity.ViewSql = sql;
        entity.ConfigJson = BuildConfig(dataSourceId, schemaName, viewName, sql);
        entity.Remark = ApplicationDataCenterCodePolicy.NormalizeOptional(request.Remark, 2000);
        entity.UpdatedBy = workspace.UserId;
        entity.UpdatedTime = DateTime.UtcNow;
        entity.VersionNo += 1;
        try
        {
            await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteViewAuditAsync(dataSource, entity.Id, "UPDATE", viewName, sql, true, null, CancellationToken.None);
        }
        catch (Exception exception)
        {
            var recoveryFailure = await PersistViewRecoveryAsync(dataSource, entity.Id, "UPDATE", viewName, sql, exception);
            if (recoveryFailure is not null)
                throw recoveryFailure;
            throw;
        }
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(
        string dataSourceId,
        string viewId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, workspace, dataSourceId, cancellationToken);
        EnsureWritableDatabase(dataSource);
        var entity = await EnsureViewAsync(appDb, workspace, dataSourceId, viewId, cancellationToken);
        var viewName = entity.RuntimeViewCode ?? entity.ObjectCode;
        await auditWriter.EnsureAvailableAsync(cancellationToken);
        var externalStarted = false;
        try
        {
            externalStarted = true;
            await DropPhysicalViewAsync(dataSource, entity.ViewSchemaName, viewName, cancellationToken);
        }
        catch (Exception exception)
        {
            await WriteViewAuditAsync(
                dataSource,
                entity.Id,
                "DELETE",
                viewName,
                entity.ViewSql,
                false,
                exception,
                CancellationToken.None,
                externalStarted ? "ManualRecovery" : null,
                externalStarted ? "ExternalDdlUnknown" : null);
            throw;
        }

        try
        {
            entity.IsDeleted = true;
            entity.DeletedBy = workspace.UserId;
            entity.DeletedTime = DateTime.UtcNow;
            await appDb.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteViewAuditAsync(dataSource, entity.Id, "DELETE", viewName, entity.ViewSql, true, null, CancellationToken.None);
        }
        catch (Exception exception)
        {
            var recoveryFailure = await PersistViewRecoveryAsync(dataSource, entity.Id, "DELETE", viewName, entity.ViewSql, exception);
            if (recoveryFailure is not null)
                throw recoveryFailure;
            throw;
        }
        return true;
    }

    public async Task<ApplicationDataCenterPreviewResponse> PreviewSqlAsync(
        string dataSourceId,
        ApplicationDataSourceSqlPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, workspace, dataSourceId, cancellationToken);
        EnsureDatabase(dataSource);
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        return await previewReader.PreviewDatabaseAsync(db, ApplicationDataSourceSqlPolicy.RequireSelectSql(request.Sql), null, request.MaxRows, cancellationToken);
    }

    private async Task WriteViewAuditAsync(
        ApplicationDataSourceEntity dataSource,
        string viewId,
        string operation,
        string viewName,
        string? sql,
        bool success,
        Exception? exception,
        CancellationToken cancellationToken,
        string? outcome = null,
        string? failureCode = null)
    {
        var statement = $"{operation} VIEW {viewName}";
        var hash = Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sql ?? statement))).ToLowerInvariant();
        await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
            TraceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
            SourceKind = "DataSourceView",
            SourceId = viewId,
            SourceName = viewName,
            DataSourceId = dataSource.Id,
            ScriptHash = hash,
            RequestHash = hash,
            ScriptPreview = statement,
            StatementSummary = operation,
            RiskSummary = "view-ddl",
            Operation = $"view.{operation.ToLowerInvariant()}",
            ResourceKind = "database.view",
            PermissionCode = ResolveViewPermissionCode(operation),
            Outcome = outcome ?? (success ? "Succeeded" : exception is OperationCanceledException ? "Canceled" : "Failed"),
            FailureCode = success ? null : failureCode ?? exception?.GetType().Name,
            Provider = dataSource.ObjectType,
            TimeoutMs = 30_000,
            CancellationRequested = exception is OperationCanceledException,
            IsSuccess = success,
            ErrorMessage = exception?.Message,
            RedactedDetailsJson = "{\"sqlStored\":false}"
        }, cancellationToken);
    }

    private static string ResolveViewPermissionCode(string operation) => operation switch
    {
        "CREATE" => PermissionCodes.AppDataCenterQueryDatasetAdd,
        "UPDATE" => PermissionCodes.AppDataCenterQueryDatasetEdit,
        "DELETE" => PermissionCodes.AppDataCenterQueryDatasetDelete,
        _ => throw new InvalidOperationException($"Unsupported view audit operation: {operation}")
    };

    private async Task<Exception?> PersistViewRecoveryAsync(
        ApplicationDataSourceEntity dataSource,
        string viewId,
        string operation,
        string viewName,
        string? sql,
        Exception exception)
    {
        try
        {
            await WriteViewAuditAsync(
                dataSource,
                viewId,
                operation,
                viewName,
                sql,
                false,
                exception,
                CancellationToken.None,
                "ManualRecovery",
                "ExternalApplicationConsistencyUnknown");
            return null;
        }
        catch (Exception auditException)
        {
            return new AggregateException(exception, auditException);
        }
    }

    private async Task CreatePhysicalViewAsync(
        ApplicationDataSourceEntity dataSource,
        string? schemaName,
        string viewName,
        string sql,
        CancellationToken cancellationToken)
    {
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var view = ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, schemaName, viewName);
        var provider = providerRegistry.Resolve(dataSource.ObjectType);
        await db.Ado.ExecuteCommandAsync(provider.BuildCreateViewSql(view, sql), Array.Empty<SugarParameter>(), cancellationToken);
    }

    private async Task DropPhysicalViewAsync(ApplicationDataSourceEntity dataSource, string? schemaName, string viewName, CancellationToken cancellationToken)
    {
        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var view = ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, schemaName, viewName);
        await db.Ado.ExecuteCommandAsync(providerRegistry.Resolve(dataSource.ObjectType).BuildDropViewSql(view), Array.Empty<SugarParameter>(), cancellationToken);
    }

    private async Task ReplacePhysicalViewAsync(
        ApplicationDataSourceEntity dataSource,
        string? oldSchemaName,
        string oldViewName,
        string? oldSql,
        string? schemaName,
        string viewName,
        string sql,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var capability = providerRegistry.Resolve(dataSource.ObjectType).Capability;
        var provider = providerRegistry.Resolve(dataSource.ObjectType);
        if (!capability.SupportsTransactionalDdl && string.IsNullOrWhiteSpace(oldSql))
        {
            throw new ValidationException("当前数据库不支持事务 DDL，且旧视图缺少可恢复 SQL，拒绝替换", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        using var db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var candidateName = $"{viewName}_candidate_{Guid.NewGuid():N}";
        var candidate = ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, schemaName, candidateName);
        var oldView = ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, oldSchemaName, oldViewName);
        var nextView = ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, schemaName, viewName);
        var recoveryToken = CancellationToken.None;
        var transactionStarted = false;
        Exception? operationException = null;
        try
        {
            if (capability.SupportsTransactionalDdl)
            {
                await db.Ado.BeginTranAsync();
                transactionStarted = true;
            }

            await db.Ado.ExecuteCommandAsync(provider.BuildCreateViewSql(candidate, sql), Array.Empty<SugarParameter>(), cancellationToken);
            await ExecuteDataTableAsync(db, provider.BuildValidateViewSql(candidate), Array.Empty<SugarParameter>(), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await db.Ado.ExecuteCommandAsync(provider.BuildDropViewSql(oldView), Array.Empty<SugarParameter>(), cancellationToken);
            await db.Ado.ExecuteCommandAsync(
                capability.SupportsAtomicViewReplace
                    ? provider.BuildCreateOrReplaceViewSql(nextView, sql)
                    : provider.BuildCreateViewSql(nextView, sql),
                Array.Empty<SugarParameter>(),
                cancellationToken);

            if (transactionStarted)
                await db.Ado.CommitTranAsync();
        }
        catch (Exception exception)
        {
            Exception? recoveryException = null;
            if (transactionStarted)
            {
                try
                {
                    await db.Ado.RollbackTranAsync();
                }
                catch (Exception rollbackException)
                {
                    recoveryException = rollbackException;
                }
            }
            else if (!string.IsNullOrWhiteSpace(oldSql))
            {
                try
                {
                    await db.Ado.ExecuteCommandAsync(provider.BuildDropViewSql(nextView), Array.Empty<SugarParameter>(), recoveryToken);
                    await db.Ado.ExecuteCommandAsync(provider.BuildCreateViewSql(oldView, oldSql), Array.Empty<SugarParameter>(), recoveryToken);
                }
                catch (Exception compensationException)
                {
                    recoveryException = compensationException;
                }
            }

            operationException = recoveryException is null
                ? exception
                : new AggregateException(new[] { exception, recoveryException });
        }

        Exception? cleanupException = null;
        try
        {
            await db.Ado.ExecuteCommandAsync(provider.BuildDropViewSql(candidate), Array.Empty<SugarParameter>(), recoveryToken);
        }
        catch (Exception exception)
        {
            cleanupException = exception;
        }

        if (operationException is not null && cleanupException is not null)
            throw new AggregateException(new[] { operationException, cleanupException });
        if (operationException is not null)
            throw operationException;
        if (cleanupException is not null)
            throw cleanupException;
    }

    private static string BuildConfig(string dataSourceId, string? schemaName, string viewName, string sql) =>
        ApplicationDataCenterJson.Serialize(new Dictionary<string, object?>
        {
            [ApplicationDataCenterWorkbenchConfigKeys.WorkbenchKind] = ApplicationDataCenterWorkbenchConfigKeys.PhysicalView,
            [ApplicationDataCenterWorkbenchConfigKeys.DataSourceId] = dataSourceId,
            ["sourceObjectId"] = dataSourceId,
            ["viewName"] = viewName,
            ["schemaName"] = schemaName,
            ["sql"] = sql
        });

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

    private static ApplicationDataSourceViewResponse Map(ApplicationQueryDatasetEntity entity)
    {
        return new ApplicationDataSourceViewResponse(
            entity.Id,
            entity.RuntimeViewCode ?? entity.ObjectCode,
            entity.ViewSchemaName,
            entity.ObjectName,
            entity.ObjectCode,
            entity.Status,
            entity.ViewSql ?? string.Empty,
            entity.Remark,
            entity.CreatedTime,
            entity.UpdatedTime,
            entity.LastValidatedAt,
            entity.LastValidationStatus,
            entity.LastValidationMessage);
    }

    private static void EnsureWritableDatabase(ApplicationDataSourceEntity dataSource)
    {
        EnsureDatabase(dataSource);
        if (dataSource.IsReadOnly)
        {
            throw new ValidationException("当前数据源为只读，不能维护视图", ErrorCodes.ApplicationDataCenterInvalidConfig);
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
        ApplicationDataCenterWorkspace workspace,
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

    private static async Task<ApplicationQueryDatasetEntity> EnsureViewAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string dataSourceId,
        string viewId,
        CancellationToken cancellationToken)
    {
        return await db.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item =>
                item.Id == viewId &&
                item.SourceObjectId == dataSourceId &&
                item.IsPhysicalView &&
                !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("视图不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private static async Task EnsureUniqueViewCodeAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string dataSourceId,
        string viewName,
        string? currentId,
        CancellationToken cancellationToken)
    {
        var objectCode = ApplicationDataCenterCodePolicy.NormalizeCode(viewName, "视图编码");
        var exists = await db.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item =>
                item.SourceObjectId == dataSourceId &&
                item.ModuleKey == ApplicationDataCenterModuleKey.QueryDataset &&
                item.ObjectCode == objectCode &&
                item.Id != (currentId ?? string.Empty) &&
                item.IsPhysicalView &&
                !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (exists)
        {
            throw new ValidationException("同一数据源下已存在同名视图", ErrorCodes.ApplicationDataCenterDuplicateCode);
        }
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;
}
