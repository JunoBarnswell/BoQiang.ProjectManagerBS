using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using System.Data;
using System.Data.Common;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationQueryDatasetService(
    IRepository<ApplicationQueryDatasetEntity> repository,
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    IApplicationDataSecretProtector secretProtector,
    ApplicationDataCenterRiskGuard riskGuard,
    ApplicationObjectReferenceService referenceService,
    ApplicationDataCenterTemplateCatalog templateCatalog,
    ApplicationDataCenterPublishedSnapshotService snapshotService,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ICurrentUser currentUser,
    ApplicationDataSourceProviderRegistry providerRegistry,
    ApplicationQueryPlanCompiler? queryPlanCompiler = null,
    ApplicationDataCenterSqlScriptAuditWriter? auditWriter = null,
    ApplicationDataMutationLedgerService? mutationLedgerService = null,
    ApplicationQueryPlanResourceResolver? resourceResolver = null)
    : ApplicationDataCenterObjectService<ApplicationQueryDatasetEntity>(
        repository,
        databaseAccessor,
        workspaceResolver,
        secretProtector,
        riskGuard,
        referenceService,
        templateCatalog,
        snapshotService)
{
    protected override string ModuleKey => ApplicationDataCenterModuleKey.QueryDataset;

    protected override void ApplySpecificFields(
        ApplicationQueryDatasetEntity entity,
        ApplicationDataCenterObjectUpsertRequest request,
        bool isCreate)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(request.ConfigJson);
        entity.SourceObjectId = RequireQueryPlan(config).DataSourceId;
        entity.RuntimeViewId = ReadString(config, "runtimeViewId");
        entity.RuntimeViewCode = ReadString(config, "runtimeViewCode") ?? entity.ObjectCode;
    }

    public override async Task<ApplicationDataCenterPreviewResponse> PreviewAsync(
        string id,
        ApplicationDataCenterPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var entity = await db.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item => item.ModuleKey == ModuleKey && item.Id == id && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("Query dataset does not exist.", ErrorCodes.ApplicationDataCenterObjectNotFound);
        var plan = RequireQueryPlan(ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson));
        plan.AccessMode = ApplicationQueryPlanAccessMode.ReadOnly;
        plan.RiskConfirmed = false;
        plan.RiskConfirmationId = null;
        return (await ExecuteQueryPlanAsync(plan, cancellationToken)).Data;
    }

    public async Task<ApplicationDataCenterRuntimeQueryResponse> QueryRuntimeAsync(
        string id,
        ApplicationDataCenterRuntimeQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var appDb = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        _ = await appDb.Queryable<ApplicationQueryDatasetEntity>()
            .Where(item => item.ModuleKey == ModuleKey && item.Id == id && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("Query dataset does not exist.", ErrorCodes.ApplicationDataCenterObjectNotFound);
        var snapshot = await GetLatestPublishedSnapshotAsync(appDb, ModuleKey, id, cancellationToken);
        var plan = RequireQueryPlan(ApplicationDataCenterJson.DeserializeDictionary(snapshot.ConfigJson));
        plan.AccessMode = ApplicationQueryPlanAccessMode.ReadOnly;
        plan.RiskConfirmed = false;
        plan.RiskConfirmationId = null;
        plan.Page = new ApplicationQueryPlanPage
        {
            Index = Math.Max(request.PageIndex, 1),
            Size = Math.Max(request.PageSize, 1)
        };
        plan.Filters = AddRuntimeFilters(plan, request.Filters);
        plan.Sorts = request.Sorts
            .Select(item => new ApplicationQueryPlanSort(item.FieldResourceId, item.Direction))
            .ToArray();
        var result = await ExecuteQueryPlanAsync(plan, cancellationToken);
        return new(
            result.Total,
            result.Data.Page?.PageIndex ?? plan.Page.Index,
            result.Data.Page?.PageSize ?? plan.Page.Size,
            result.Data.Fields,
            result.Data.Rows,
            snapshot.Id,
            snapshot.VersionNo);
    }

    public async Task<ApplicationQueryPlanDiagnosticResponse> DiagnoseQueryPlanAsync(
        ApplicationQueryPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var auditId = ResolveAuditId();
        try
        {
            var source = await RequireDataSourceAsync(request.DataSourceId, cancellationToken);
            var provider = ResolveProvider(source.ObjectType);
            var model = await ResolveQueryModelAsync(request, cancellationToken);
            var compiled = (queryPlanCompiler ?? new ApplicationQueryPlanCompiler()).Compile(request, model, provider, cancellationToken);
            return new(true, compiled.Provider, compiled.PageSql, [], BuildWarnings(request), auditId);
        }
        catch (ValidationException exception)
        {
            return new(false, null, null, [exception.Message], [], auditId);
        }
    }

    public async Task<ApplicationQueryPlanResponse> PreviewQueryPlanAsync(
        ApplicationQueryPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(request.AccessMode, ApplicationQueryPlanAccessMode.ControlledWrite, StringComparison.OrdinalIgnoreCase))
        {
            var plan = await DiagnoseQueryPlanAsync(request, cancellationToken);
            return new(new([], [], "Controlled write preview does not execute a mutation."), plan, 0, plan.AuditId ?? ResolveAuditId());
        }
        var result = await ExecuteQueryPlanAsync(request, cancellationToken);
        return result;
    }

    public Task<ApplicationQueryPlanResponse> ExecuteQueryPlanAsync(
        ApplicationQueryPlanRequest request,
        CancellationToken cancellationToken = default) => ExecuteQueryPlanCoreAsync(request, cancellationToken);

    public Task<ApplicationQueryPlanResponse> ExecuteControlledWriteQueryPlanAsync(
        ApplicationQueryPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.AccessMode, ApplicationQueryPlanAccessMode.ControlledWrite, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Controlled write endpoint requires controlledWrite access mode.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        return ExecuteQueryPlanCoreAsync(request, cancellationToken);
    }

    private async Task<ApplicationQueryPlanResponse> ExecuteQueryPlanCoreAsync(ApplicationQueryPlanRequest request, CancellationToken cancellationToken)
    {
        var source = await RequireDataSourceAsync(request.DataSourceId, cancellationToken);
        var provider = ResolveProvider(source.ObjectType);
        var compiler = queryPlanCompiler ?? new ApplicationQueryPlanCompiler();
        var model = await ResolveQueryModelAsync(request, cancellationToken);
        var compiled = compiler.Compile(request, model, provider, cancellationToken);
        var auditId = ResolveAuditId();
        var isControlledWrite = compiled.WriteSql is not null;
        var requestHash = isControlledWrite ? ComputeRequestHash(request) : null;
        ApplicationDataMutationLedgerEntity? ledger = null;
        var externalWriteCommitted = false;
        var externalAffectedRows = 0;
        var ledgerService = mutationLedgerService ?? new ApplicationDataMutationLedgerService(DatabaseAccessor, WorkspaceResolver);
        if (isControlledWrite)
        {
            if (auditWriter is null)
            {
                throw new ValidationException(
                    "Controlled QueryPlan writes require an available audit sink.",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            await EnsureWriteAuthorizationAsync(request, source, requestHash!, cancellationToken);
            await auditWriter.EnsureAvailableAsync(cancellationToken);
            var reservation = await ledgerService.ReserveAsync(
                new(
                    compiled.StatementKind,
                    requestHash!,
                    "query-plan",
                    request.DataSourceId,
                    request.DataSourceId,
                    request.DataSourceId,
                    compiled.StatementKind,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(compiled.WriteSql!))).ToLowerInvariant(),
                    compiled.Provider,
                    request.ExpectedAffectedRows),
                cancellationToken);
            ledger = reservation.Ledger;
            if (!reservation.IsNew)
            {
                var status = ledger.Status is ApplicationDataMutationLedgerStatus.Executing or ApplicationDataMutationLedgerStatus.Unknown
                    ? ApplicationDataMutationLedgerStatus.RecoveryRequired
                    : ledger.Status;
                var replayPlan = new ApplicationQueryPlanDiagnosticResponse(true, compiled.Provider, compiled.WriteSql, [], BuildWarnings(request), auditId);
                return new(
                    new([], [], status == ApplicationDataMutationLedgerStatus.RecoveryRequired
                        ? "Controlled write requires manual recovery."
                        : $"Controlled write was already recorded as {ledger.Status}."),
                    replayPlan,
                    ledger.AffectedRows,
                    auditId,
                    requestHash,
                    ledger.Id,
                    status,
                    status == ApplicationDataMutationLedgerStatus.RecoveryRequired);
            }
        }
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = Math.Clamp(request.TimeoutSeconds <= 0 ? 30 : request.TimeoutSeconds, 1, 300);
        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        var executionToken = timeout.Token;
        var started = global::System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var sourceDb = await connectionFactory.CreateDatabaseClientAsync(source, executionToken);
            sourceDb.Ado.CommandTimeOut = timeoutSeconds;
            if (compiled.WriteSql is not null)
            {
                var execution = await ExecuteControlledWriteAsync(sourceDb, provider, compiled, request, model, executionToken);
                externalWriteCommitted = !execution.Succeeded && !execution.OutcomeKnown;
                externalAffectedRows = execution.AffectedRows;
                if (!execution.Succeeded)
                {
                    var failureStatus = execution.OutcomeKnown
                        ? ApplicationDataMutationLedgerStatus.Failed
                        : ApplicationDataMutationLedgerStatus.Unknown;
                    await ledgerService.TransitionAsync(
                        ledger!.Id,
                        failureStatus,
                        execution.AffectedRows,
                        execution.OutcomeKnown ? "ControlledWriteFailed" : "ControlledWriteUnknown",
                        execution.Error?.Message,
                        execution.OutcomeKnown ? "External DML failed and was rolled back." : "External DML outcome is unknown; manual recovery is required.",
                        null,
                        CancellationToken.None);
                    await WriteQueryAuditAsync(request, source, auditId, started.ElapsedMilliseconds, 0, execution.AffectedRows, false, execution.Error?.Message, CancellationToken.None, compiled.StatementKind, compiled.WriteSql, outcome: execution.OutcomeKnown ? "Failed" : "Unknown", failureCode: execution.OutcomeKnown ? "ControlledWriteFailed" : "ControlledWriteUnknown");
                    if (!execution.OutcomeKnown)
                    {
                        var recoveryPlan = new ApplicationQueryPlanDiagnosticResponse(true, compiled.Provider, compiled.WriteSql, [], BuildWarnings(request), auditId);
                        return new(
                            new([], [], "Controlled write outcome is unknown; manual recovery is required."),
                            recoveryPlan,
                            execution.AffectedRows,
                            auditId,
                            requestHash,
                            ledger.Id,
                            ApplicationDataMutationLedgerStatus.RecoveryRequired,
                            true);
                    }

                    throw execution.Error!;
                }

                var empty = new ApplicationDataCenterPreviewResponse([], [], "Controlled write executed.");
                var writePlan = new ApplicationQueryPlanDiagnosticResponse(true, compiled.Provider, compiled.WriteSql, [], BuildWarnings(request), auditId);
                externalWriteCommitted = true;
                externalAffectedRows = execution.AffectedRows;
                await WriteQueryAuditAsync(request, source, auditId, started.ElapsedMilliseconds, 0, execution.AffectedRows, true, null, CancellationToken.None, compiled.StatementKind, compiled.WriteSql);
                await ledgerService.TransitionAsync(
                    ledger!.Id,
                    ApplicationDataMutationLedgerStatus.Finalized,
                    execution.AffectedRows,
                    null,
                    null,
                    "External DML committed and application ledger finalized.",
                    null,
                    CancellationToken.None);
                return new(empty, writePlan, execution.AffectedRows, auditId, requestHash, ledger.Id, ApplicationDataMutationLedgerStatus.Finalized, false);
            }
            var countSql = ApplicationDataSourceSqlPolicy.RequireSelectSql(compiled.CountSql, "QueryPlan count SQL");
            var pageSql = ApplicationDataSourceSqlPolicy.RequireSelectSql(compiled.PageSql, "QueryPlan page SQL");
            var total = Convert.ToInt32(await ExecuteScalarAsync(sourceDb, countSql, compiled.Parameters.ToArray(), executionToken) ?? 0);
            executionToken.ThrowIfCancellationRequested();
            var rows = await ExecuteDataTableAsync(sourceDb, pageSql, compiled.Parameters.ToArray(), executionToken);
            var data = ApplicationDataPreviewReader.FromDataTable(rows, "QueryPlan 查询成功");
            var plan = new ApplicationQueryPlanDiagnosticResponse(true, compiled.Provider, pageSql, [], BuildWarnings(request), auditId);
            await WriteQueryAuditAsync(request, source, auditId, started.ElapsedMilliseconds, data.Rows.Count, 0, true, null, CancellationToken.None, compiled.StatementKind, pageSql);
            return new(data, plan, total, auditId);
        }
        catch (Exception exception)
        {
            if (externalWriteCommitted && ledger is not null)
            {
                var recoveryFailure = await PersistQueryRecoveryAsync(
                    ledgerService,
                    ledger.Id,
                    request,
                    source,
                    auditId,
                    started.ElapsedMilliseconds,
                    externalAffectedRows,
                    exception,
                    compiled.StatementKind,
                    compiled.WriteSql ?? compiled.PageSql,
                    requestHash);
                if (recoveryFailure is not null)
                    throw recoveryFailure;
            }
            else
            {
                await WriteQueryAuditAsync(request, source, auditId, started.ElapsedMilliseconds, 0, 0, false, exception.Message, CancellationToken.None, compiled.StatementKind, compiled.WriteSql ?? compiled.PageSql);
            }
            throw;
        }
    }

    private async Task<ControlledWriteExecution> ExecuteControlledWriteAsync(
        ISqlSugarClient db,
        IApplicationDataSourceProvider provider,
        ApplicationQueryPlanCompiler.CompiledPlan compiled,
        ApplicationQueryPlanRequest request,
        ApplicationQueryPlanResolvedModel model,
        CancellationToken cancellationToken)
    {
        var operation = compiled.StatementKind;
        var transactionStarted = false;
        var commandStarted = false;
        var commitAttempted = false;
        try
        {
            var where = request.Filters.Count == 0 ? string.Empty : BuildWriteWhere(request, model, provider);
            var estimated = operation == "INSERT"
                ? 1
                : Convert.ToInt32(await ExecuteScalarAsync(db, provider.BuildCountSql(provider.QuoteQualified(model.Nodes[0].Resource.SchemaName, model.Nodes[0].Resource.ObjectName), where), compiled.Parameters.ToArray(), cancellationToken) ?? 0);
            var maxRows = Math.Min(request.RowLimit <= 0 ? provider.Capability.MaxWriteRows : request.RowLimit, provider.Capability.MaxWriteRows);
            if (estimated > maxRows)
                throw new ValidationException($"Controlled write impact {estimated} exceeds maximum {maxRows}.", ErrorCodes.PermissionDenied);
            if (request.ExpectedAffectedRows is null || request.ExpectedAffectedRows.Value != estimated)
                throw new ValidationException($"Controlled write impact is {estimated}; ExpectedAffectedRows must match.", ErrorCodes.ApplicationDataCenterInvalidConfig);

            await db.Ado.BeginTranAsync();
            transactionStarted = true;
            cancellationToken.ThrowIfCancellationRequested();
            commandStarted = true;
            var affected = await db.Ado.ExecuteCommandAsync(compiled.WriteSql!, compiled.Parameters.ToArray(), cancellationToken);
            if (affected > maxRows || affected != estimated)
                throw new ValidationException("Controlled write affected-row count changed during execution.", ErrorCodes.ApplicationDataCenterInvalidConfig);
            commitAttempted = true;
            cancellationToken.ThrowIfCancellationRequested();
            await db.Ado.CommitTranAsync();
            return new(true, affected, null, true);
        }
        catch (Exception exception)
        {
            if (!transactionStarted)
                return new(false, 0, exception, true);

            try
            {
                await db.Ado.RollbackTranAsync();
                return new(false, 0, exception, !commitAttempted);
            }
            catch (Exception rollbackException)
            {
                return new(false, 0, new AggregateException(exception, rollbackException), commandStarted && !commitAttempted ? false : true);
            }
        }
    }

    private sealed record ControlledWriteExecution(bool Succeeded, int AffectedRows, Exception? Error, bool OutcomeKnown);

    private static string BuildWriteWhere(ApplicationQueryPlanRequest request, ApplicationQueryPlanResolvedModel model, IApplicationDataSourceProvider provider)
    {
        var node = model.Nodes[0];
        var clauses = request.Filters.Select(filter =>
        {
            var field = node.Resource.Fields.FirstOrDefault(item => string.Equals(item.ResourceId, filter.FieldResourceId, StringComparison.Ordinal));
            var parameter = request.Parameters.FirstOrDefault(item => string.Equals(item.ResourceId, filter.ParameterResourceId, StringComparison.Ordinal));
            if (field is null || parameter is null) throw new ValidationException("Write filter must use field and parameter Resource IDs.", ErrorCodes.ApplicationDataCenterInvalidConfig);
            var op = filter.Operator.ToLowerInvariant() switch { "eq" => "=", "ne" => "<>", "gt" => ">", "gte" => ">=", "lt" => "<", "lte" => "<=", _ => throw new ValidationException("Controlled write filters only support comparison operators.", ErrorCodes.ApplicationDataCenterInvalidConfig) };
            return $"{provider.QuoteIdentifier(field.Name)} {op} {provider.BuildParameterName(parameter.Name.Trim())}";
        });
        return " WHERE " + string.Join(" AND ", clauses);
    }

    private async Task<ApplicationDataSourceEntity> RequireDataSourceAsync(string dataSourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dataSourceId))
            throw new ValidationException("QueryPlan 缺少数据源", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return await db.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId && !item.IsDeleted)
            .FirstAsync(cancellationToken)
            ?? throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private async Task WriteQueryAuditAsync(ApplicationQueryPlanRequest request, ApplicationDataSourceEntity source, string auditId, long duration, int rows, int affectedRows, bool success, string? error, CancellationToken cancellationToken, string statementKind, string? sql, string sourceKind = "QueryPlan", string? requestHash = null, DateTime? expiresAt = null, string? outcome = null, string? failureCode = null)
    {
        if (auditWriter is null) return;
        await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
            Id = sourceKind == "QueryPlanRiskConfirmation" ? auditId : Guid.NewGuid().ToString("N"),
            TraceId = auditId,
            SourceKind = sourceKind,
            SourceId = source.Id,
            SourceName = source.ObjectName,
            DataSourceId = source.Id,
            ScriptHash = Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(sql ?? request.DataSourceId))).ToLowerInvariant(),
            ScriptPreview = string.IsNullOrWhiteSpace(sql) ? "QueryPlan generated SQL" : sql[..Math.Min(sql.Length, 1000)],
            StatementSummary = statementKind,
            RiskSummary = request.AccessMode,
            ParameterSummaryJson = global::System.Text.Json.JsonSerializer.Serialize(request.Parameters.Select(item => new { item.Name, item.Type })),
            DurationMs = duration,
            AffectedRows = affectedRows,
            ReturnedRows = rows,
            IsSuccess = success,
            ErrorMessage = error,
            Operation = sourceKind == "QueryPlanRiskConfirmation" ? "query.risk-confirmation.issue" : statementKind.Equals("SELECT", StringComparison.OrdinalIgnoreCase) ? "query.execute" : "query.controlled-write",
            ResourceKind = "query-plan",
            Outcome = outcome ?? (sourceKind == "QueryPlanRiskConfirmation" ? "Pending" : success ? "Succeeded" : "Failed"),
            FailureCode = success || string.IsNullOrWhiteSpace(error) ? null : failureCode ?? "QueryExecutionFailed",
            Provider = source.ObjectType,
            TimeoutMs = Math.Clamp(request.TimeoutSeconds <= 0 ? 30 : request.TimeoutSeconds, 1, 300) * 1000,
            RequestHash = requestHash ?? Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(global::System.Text.Encoding.UTF8.GetBytes(sql ?? request.DataSourceId))).ToLowerInvariant(),
            RedactedDetailsJson = expiresAt.HasValue
                ? global::System.Text.Json.JsonSerializer.Serialize(new RiskConfirmationDetails(expiresAt.Value))
                : global::System.Text.Json.JsonSerializer.Serialize(new { request.RowLimit, request.Page.Size, returnedRows = rows, affectedRows })
        }, cancellationToken);
    }

    private async Task<Exception?> PersistQueryRecoveryAsync(
        ApplicationDataMutationLedgerService ledgerService,
        string ledgerId,
        ApplicationQueryPlanRequest request,
        ApplicationDataSourceEntity source,
        string auditId,
        long duration,
        int affectedRows,
        Exception exception,
        string statementKind,
        string? sql,
        string? requestHash)
    {
        var errors = new List<Exception>();
        try
        {
            await ledgerService.TransitionAsync(
                ledgerId,
                ApplicationDataMutationLedgerStatus.Unknown,
                affectedRows,
                "ControlledWriteUnknown",
                exception.Message,
                "External DML completed or may have completed, but application audit/finalization failed; manual recovery is required.",
                null,
                CancellationToken.None);
        }
        catch (Exception ledgerException)
        {
            errors.Add(ledgerException);
        }

        try
        {
            await WriteQueryAuditAsync(
                request,
                source,
                Guid.NewGuid().ToString("N"),
                duration,
                0,
                affectedRows,
                false,
                exception.Message,
                CancellationToken.None,
                statementKind,
                sql,
                "QueryPlanRecovery",
                requestHash,
                outcome: "Unknown",
                failureCode: "ControlledWriteUnknown");
        }
        catch (Exception auditException)
        {
            errors.Add(auditException);
        }

        return errors.Count == 0 ? null : new AggregateException(new[] { exception }.Concat(errors));
    }

    private static async Task<object?> ExecuteScalarAsync(
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
        AddParameters(command, parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

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
        AddParameters(command, parameters);
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

    private static void AddParameters(DbCommand command, IReadOnlyList<SugarParameter> parameters)
    {
        foreach (var parameter in parameters)
        {
            var dbParameter = command.CreateParameter();
            dbParameter.ParameterName = parameter.ParameterName;
            dbParameter.Value = parameter.Value ?? DBNull.Value;
            command.Parameters.Add(dbParameter);
        }
    }

    public async Task<ApplicationQueryPlanRiskConfirmationResponse> IssueRiskConfirmationAsync(
        ApplicationQueryPlanRiskConfirmationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Plan is null || !string.Equals(request.Plan.AccessMode, ApplicationQueryPlanAccessMode.ControlledWrite, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Risk confirmation requires controlledWrite access mode.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        if (!currentUser.HasAsterErpPermission(PermissionCodes.AppDataCenterQueryDatasetEdit))
            throw new ValidationException("Controlled QueryPlan writes require query-dataset edit permission.", ErrorCodes.PermissionDenied);

        var source = await RequireDataSourceAsync(request.Plan.DataSourceId, cancellationToken);
        var provider = ResolveProvider(source.ObjectType);
        var model = await ResolveQueryModelAsync(request.Plan, cancellationToken);
        var compiled = (queryPlanCompiler ?? new ApplicationQueryPlanCompiler()).Compile(request.Plan, model, provider, cancellationToken);
        var requestHash = ComputeRequestHash(request.Plan);
        var expiresAt = DateTime.UtcNow.AddSeconds(Math.Clamp(request.ExpiresInSeconds <= 0 ? 120 : request.ExpiresInSeconds, 30, 300));
        var confirmationId = Guid.NewGuid().ToString("N");
        await WriteQueryAuditAsync(request.Plan, source, confirmationId, 0, 0, 0, false, null, cancellationToken, "RISK_CONFIRMATION_ISSUED", compiled.WriteSql, "QueryPlanRiskConfirmation", requestHash, expiresAt);
        return new(confirmationId, requestHash, expiresAt);
    }

    private async Task EnsureWriteAuthorizationAsync(ApplicationQueryPlanRequest request, ApplicationDataSourceEntity source, string requestHash, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.AccessMode, ApplicationQueryPlanAccessMode.ControlledWrite, StringComparison.OrdinalIgnoreCase)) return;
        if (!currentUser.HasAsterErpPermission(PermissionCodes.AppDataCenterQueryDatasetEdit))
        {
            await WriteQueryAuditAsync(request, source, ResolveAuditId(), 0, 0, 0, false, "PermissionDenied", CancellationToken.None, "CONTROLLED_WRITE_DENIED", null);
            throw new ValidationException("Controlled QueryPlan writes require query-dataset edit permission.", ErrorCodes.PermissionDenied);
        }
        if (!request.RiskConfirmed || string.IsNullOrWhiteSpace(request.RiskConfirmationId))
        {
            throw new ValidationException("Controlled QueryPlan writes require a server-issued risk confirmation.", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);
        }

        var workspace = WorkspaceResolver.Resolve();
        var confirmation = request.RiskConfirmationId.Trim();
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var issued = await db.Queryable<ApplicationSqlScriptAuditEntity>()
            .Where(item => item.Id == confirmation && !item.IsDeleted && item.ActorUserId == workspace.UserId && item.SourceKind == "QueryPlanRiskConfirmation" && item.Outcome == "Pending" && item.RequestHash == requestHash)
            .FirstAsync(cancellationToken);
        if (issued is null || !TryReadExpiration(issued.RedactedDetailsJson, out var expiresAt) || expiresAt <= DateTime.UtcNow)
            throw new ValidationException("Risk confirmation is invalid, expired, or does not match this request.", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);

        var consumed = await db.Updateable<ApplicationSqlScriptAuditEntity>()
            .SetColumns(item => new ApplicationSqlScriptAuditEntity { Outcome = "Consumed", UpdatedBy = workspace.UserId, UpdatedTime = DateTime.UtcNow })
            .Where(item => item.Id == confirmation && !item.IsDeleted && item.ActorUserId == workspace.UserId && item.SourceKind == "QueryPlanRiskConfirmation" && item.Outcome == "Pending" && item.RequestHash == requestHash)
            .ExecuteCommandAsync(cancellationToken);
        if (consumed != 1)
            throw new ValidationException("Risk confirmation is invalid, expired, or has already been consumed.", ErrorCodes.ApplicationDataCenterRiskConfirmationRequired);
    }

    private static string ResolveAuditId() => Guid.NewGuid().ToString("N");

    private Task<ApplicationQueryPlanResolvedModel> ResolveQueryModelAsync(ApplicationQueryPlanRequest request, CancellationToken cancellationToken)
    {
        var resolver = resourceResolver ?? new ApplicationQueryPlanResourceResolver(DatabaseAccessor);
        return resolver.ResolveAsync(request, cancellationToken);
    }
    private static string ComputeRequestHash(ApplicationQueryPlanRequest request) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { request.DataSourceId, request.Nodes, request.Joins, request.Columns, request.Filters, request.GroupBy, request.Having, request.Sorts, request.Page, request.Parameters, request.AccessMode, request.TimeoutSeconds, request.RowLimit, request.WriteOperation, request.WriteValues, request.ExpectedAffectedRows })))).ToLowerInvariant();
    private static bool TryReadExpiration(string json, out DateTime expiresAt)
    {
        expiresAt = default;
        try { expiresAt = JsonSerializer.Deserialize<RiskConfirmationDetails>(json)?.ExpiresAt ?? default; return expiresAt != default; }
        catch (JsonException) { return false; }
    }
    private sealed record RiskConfirmationDetails(DateTime ExpiresAt);
    private static IReadOnlyList<string> BuildWarnings(ApplicationQueryPlanRequest request) => request.Page.Size > request.RowLimit && request.RowLimit > 0 ? ["分页大小已按行上限截断"] : [];

    protected override async Task ValidateForPublishAsync(ApplicationQueryDatasetEntity entity, CancellationToken cancellationToken)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        if (config.ContainsKey("queryPlan"))
        {
            var latestPlan = RequireQueryPlan(config);
            var latestDb = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
            _ = await GetLatestPublishedSnapshotAsync(latestDb, ApplicationDataCenterModuleKey.DataSource, latestPlan.DataSourceId, cancellationToken);
            _ = await ResolveQueryModelAsync(latestPlan, cancellationToken);
            return;
        }
        var sourceId = ReadString(config, "sourceObjectId") ?? ReadString(config, "dataSourceId");
        var tableName = ReadString(config, "tableName");
        if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(tableName))
            throw new ValidationException("查询数据集必须配置来源数据源和表", ErrorCodes.ApplicationDataCenterInvalidConfig);
        var db = await DatabaseAccessor.RequireApplicationDbAsync(cancellationToken);
        _ = await GetLatestPublishedSnapshotAsync(db, ApplicationDataCenterModuleKey.DataSource, sourceId, cancellationToken);
        throw new ValidationException("Query dataset publish requires the latest Resource ID QueryPlan.", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    protected override IReadOnlyDictionary<string, object?> BuildSnapshotBinding(ApplicationQueryDatasetEntity entity)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["queryPlan"] = RequireQueryPlan(config),
            ["sourceObjectId"] = entity.SourceObjectId
        };
    }
    private static ApplicationQueryPlanRequest RequireQueryPlan(IReadOnlyDictionary<string, object?> config)
    {
        if (config.TryGetValue("queryPlan", out var raw) && raw is not null)
        {
            var json = raw is JsonElement element ? element.GetRawText() : JsonSerializer.Serialize(raw, ApplicationDataCenterJson.Options);
            var plan = JsonSerializer.Deserialize<ApplicationQueryPlanRequest>(json, ApplicationDataCenterJson.Options);
            if (plan is not null && !string.IsNullOrWhiteSpace(plan.DataSourceId) && plan.Nodes.Count > 0)
                return plan;
        }

        throw new ValidationException("Query dataset must persist the latest Resource ID QueryPlan.", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> config, string key) =>
        config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static IReadOnlyList<ApplicationQueryPlanFilter> AddRuntimeFilters(
        ApplicationQueryPlanRequest plan,
        IReadOnlyList<ApplicationDataCenterRuntimeQueryFilterRequest> filters)
    {
        if (filters.Count == 0)
            return plan.Filters;
        var parameters = plan.Parameters.ToList();
        var runtimeFilters = new List<ApplicationQueryPlanFilter>(filters.Count);
        foreach (var (filter, index) in filters.Select((item, index) => (item, index)))
        {
            if (string.IsNullOrWhiteSpace(filter.FieldResourceId))
                throw new ValidationException("Runtime filters require stable field Resource IDs.", ErrorCodes.ApplicationDataCenterInvalidConfig);
            var resourceId = $"runtime-filter:{index}";
            parameters.Add(new(resourceId, $"runtime_filter_{index}", InferParameterType(filter.Value), filter.Value));
            runtimeFilters.Add(new(filter.FieldResourceId, filter.Operator, resourceId));
        }
        plan.Parameters = parameters;
        return runtimeFilters;
    }

    private static string InferParameterType(object? value) => value switch
    {
        bool => "bool",
        byte or sbyte or short or ushort or int or uint => "int",
        long or ulong => "long",
        float or double => "double",
        decimal => "decimal",
        DateTime => "dateTime",
        Guid => "guid",
        _ => "string"
    };

    private IApplicationDataSourceProvider ResolveProvider(string sourceType) => providerRegistry.Resolve(sourceType);

    private static async Task<ApplicationDataCenterPublishedSnapshot> GetLatestPublishedSnapshotAsync(
        ISqlSugarClient db,
        string moduleKey,
        string objectId,
        CancellationToken cancellationToken) =>
        await db.Queryable<ApplicationDataCenterPublishedSnapshot>()
            .Where(item => item.ModuleKey == moduleKey && item.ObjectId == objectId && !item.IsDeleted)
            .OrderBy(item => item.VersionNo, OrderByType.Desc)
            .Take(1)
            .FirstAsync(cancellationToken)
        ?? throw new ValidationException("杩愯鏃跺璞″尚未发布", ErrorCodes.ApplicationDataCenterObjectNotFound);
}
