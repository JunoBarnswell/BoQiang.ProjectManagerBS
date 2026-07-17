using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataSourceService dataSourceService,
    ApplicationDataSourceProviderRegistry providerRegistry,
    ApplicationDataCenterSqlScriptAuditWriter? auditWriter = null,
    ApplicationDataMutationLedgerService? mutationLedgerService = null)
{
    public async Task<ApplicationDataSourceTableRowsResponse> QueryRowsAsync(
        string dataSourceId,
        string tableName,
        ApplicationDataSourceTableRowsQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = await BuildContextAsync(dataSourceId, tableName, cancellationToken);
        EnsureDatabase(context.DataSource);
        using var db = await connectionFactory.CreateDatabaseClientAsync(context.DataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var provider = ResolveProvider(context.DataSource.ObjectType);

        var pageIndex = Math.Max(request.PageIndex, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, provider.Capability.MaxPageSize);
        var where = BuildWhereClause(context, provider, request.Keyword, request.Filters);
        var orderBy = BuildOrderByClause(context, request.Sorts);
        var total = Convert.ToInt32(await ExecuteScalarAsync(db, provider.BuildCountSql(context.QuotedTableName, where.Sql), where.Parameters.ToArray(), cancellationToken) ?? 0);
        var dataTable = await ExecuteDataTableAsync(
            db,
            provider.BuildPageSql($"SELECT {BuildRowSelectSql(context, provider)} FROM {context.QuotedTableName}{where.Sql}", orderBy, (pageIndex - 1) * pageSize, pageSize),
            where.Parameters.ToArray(),
            cancellationToken);
        return BuildRowsResponse(context, provider, dataTable, total, pageIndex, pageSize);
    }

    public async Task<(int TotalRows, int ExportedRows, bool Truncated)> StreamRowsExportAsync(
        string dataSourceId,
        string tableName,
        ApplicationDataSourceTableRowsExportRequest request,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        const int defaultMaxRows = 10_000;
        const int hardMaxRows = 100_000;
        var maxRows = Math.Clamp(request.MaxRows <= 0 ? defaultMaxRows : request.MaxRows, 1, hardMaxRows);
        var context = await BuildContextAsync(dataSourceId, tableName, cancellationToken);
        using var db = await connectionFactory.CreateDatabaseClientAsync(context.DataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var provider = ResolveProvider(context.DataSource.ObjectType);
        var where = BuildWhereClause(context, provider, request.Keyword, request.Filters);
        var orderBy = BuildOrderByClause(context, request.Sorts);
        var totalRows = Convert.ToInt32(await ExecuteScalarAsync(
            db,
            provider.BuildCountSql(context.QuotedTableName, where.Sql),
            where.Parameters.ToArray(),
            cancellationToken) ?? 0);
        var sql = provider.BuildPageSql(
            $"SELECT {BuildExportSelectSql(context)} FROM {context.QuotedTableName}{where.Sql}",
            orderBy,
            0,
            maxRows);

        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("The data source does not support asynchronous result reading.");
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        AddParameters(command, where.Parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), 16 * 1024, leaveOpen: true)
        {
            AutoFlush = false,
            NewLine = "\r\n"
        };
        await writer.WriteAsync('\uFEFF');
        for (var index = 0; index < reader.FieldCount; index++)
        {
            if (index > 0)
                await writer.WriteAsync(',');
            await writer.WriteAsync(EscapeCsv(reader.GetName(index)));
        }
        await writer.WriteLineAsync();

        var exportedRows = 0;
        while (exportedRows < maxRows && await reader.ReadAsync(cancellationToken))
        {
            for (var index = 0; index < reader.FieldCount; index++)
            {
                if (index > 0)
                    await writer.WriteAsync(',');
                var value = await reader.IsDBNullAsync(index, cancellationToken) ? null : reader.GetValue(index);
                await writer.WriteAsync(EscapeCsv(value));
            }
            await writer.WriteLineAsync();
            exportedRows++;
            if (exportedRows % 256 == 0)
                await writer.FlushAsync(cancellationToken);
        }

        await writer.FlushAsync(cancellationToken);
        return (totalRows, exportedRows, totalRows > exportedRows);
    }

    public async Task<ApplicationDataSourceTableRowMutationResponse> InsertRowAsync(
        string dataSourceId,
        string tableName,
        ApplicationDataSourceTableRowUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = await BuildContextAsync(dataSourceId, tableName, cancellationToken);
        EnsureInsertableTable(context);
        var values = NormalizeValues(context, request.Values, requireNonEmpty: true);
        var fields = values.Keys.Select(name => ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, name)).ToArray();
        var parameters = values.Select((pair, index) => new SugarParameter($"@p{index}", NormalizeParameterValue(pair.Value))).ToArray();
        var placeholders = parameters.Select(parameter => parameter.ParameterName).ToArray();
        var sql = $"INSERT INTO {context.QuotedTableName} ({string.Join(", ", fields)}) VALUES ({string.Join(", ", placeholders)})";
        var requestHash = ResolveRequestHash(request.RequestHash, ComputeMutationHash("INSERT", dataSourceId, tableName, null, values, null, null, request.ExpectedAffectedRows));
        var ledgerService = mutationLedgerService ?? new ApplicationDataMutationLedgerService(databaseAccessor, workspaceResolver);
        var reservation = await ledgerService.ReserveAsync(
            BuildLedgerReservation("INSERT", requestHash, context, sql, request.ExpectedAffectedRows),
            cancellationToken);
        if (!reservation.IsNew)
        {
            return BuildLedgerResponse(reservation.Ledger);
        }

        using var db = await connectionFactory.CreateDatabaseClientAsync(context.DataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var commandStarted = false;
        try
        {
            commandStarted = true;
            var affected = await db.Ado.ExecuteCommandAsync(sql, parameters, cancellationToken);
            await WriteMutationAuditAsync(context, request.AuditId, "INSERT", affected, true, null, CancellationToken.None);
            await ledgerService.TransitionAsync(reservation.Ledger.Id, ApplicationDataMutationLedgerStatus.Finalized, affected, null, null, "Table row insert committed and ledger finalized.", null, CancellationToken.None);
            return BuildLedgerResponse(reservation.Ledger, affected, ApplicationDataMutationLedgerStatus.Finalized, requestHash);
        }
        catch (Exception exception)
        {
            var status = commandStarted ? ApplicationDataMutationLedgerStatus.Unknown : ApplicationDataMutationLedgerStatus.Failed;
            var persistedFailure = await PersistMutationFailureAsync(
                ledgerService,
                reservation.Ledger.Id,
                context,
                request.AuditId,
                "INSERT",
                status,
                0,
                exception);
            if (persistedFailure is not null)
                throw persistedFailure;
            throw;
        }
    }

    public async Task<ApplicationDataSourceTableRowMutationResponse> UpdateRowAsync(
        string dataSourceId,
        string tableName,
        ApplicationDataSourceTableRowUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = await BuildContextAsync(dataSourceId, tableName, cancellationToken);
        EnsureWritableTable(context);
        EnsurePrimaryKeys(context);
        var provider = ResolveProvider(context.DataSource.ObjectType);
        var values = NormalizeValues(context, request.Values, requireNonEmpty: true)
            .Where(pair => !context.PrimaryKeys.Contains(pair.Key) &&
                           !IsVersionColumn(context.RequireColumn(pair.Key), provider))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        if (values.Count == 0)
        {
            throw new ValidationException("没有可更新的字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var parameters = new List<SugarParameter>();
        var setClauses = values.Select((pair, index) =>
        {
            var parameter = new SugarParameter($"@v{index}", NormalizeParameterValue(pair.Value));
            parameters.Add(parameter);
            return $"{ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, pair.Key)} = {parameter.ParameterName}";
        }).ToArray();
        var keyWhere = BuildPrimaryKeyWhere(context, request.KeyValues, parameters, "@k");
        var concurrency = BuildConcurrencyWhere(context, provider, request.VersionValue, request.OriginalValues, request.ConflictResolution, parameters);
        var concurrencyWhere = CombineWhere(keyWhere, concurrency.Sql);
        using var db = await connectionFactory.CreateDatabaseClientAsync(context.DataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var expected = await EnsureImpactConfirmedAsync(db, context, provider, concurrencyWhere, parameters, request.Confirmed, request.ExpectedAffectedRows, cancellationToken);
        var sql = $"UPDATE {context.QuotedTableName} SET {string.Join(", ", setClauses)} WHERE {concurrencyWhere}";
        var requestHash = ResolveRequestHash(request.RequestHash, ComputeMutationHash("UPDATE", dataSourceId, tableName, request.KeyValues, values, request.VersionValue, request.OriginalValues, request.ExpectedAffectedRows));
        var ledgerService = mutationLedgerService ?? new ApplicationDataMutationLedgerService(databaseAccessor, workspaceResolver);
        var reservation = await ledgerService.ReserveAsync(
            BuildLedgerReservation("UPDATE", requestHash, context, sql, request.ExpectedAffectedRows),
            cancellationToken);
        if (!reservation.IsNew)
        {
            return BuildLedgerResponse(reservation.Ledger);
        }

        var commandStarted = false;
        int affected;
        try
        {
            commandStarted = true;
            affected = await db.Ado.ExecuteCommandAsync(sql, parameters.ToArray(), cancellationToken);
        }
        catch (Exception exception)
        {
            var status = commandStarted ? ApplicationDataMutationLedgerStatus.Unknown : ApplicationDataMutationLedgerStatus.Failed;
            var persistedFailure = await PersistMutationFailureAsync(
                ledgerService,
                reservation.Ledger.Id,
                context,
                request.AuditId,
                "UPDATE",
                status,
                0,
                exception);
            if (persistedFailure is not null)
                throw persistedFailure;
            throw;
        }
        if (affected == expected)
        {
            try
            {
                var auditId = await WriteMutationAuditAsync(context, request.AuditId, "UPDATE", affected, true, null, CancellationToken.None);
                await ledgerService.TransitionAsync(reservation.Ledger.Id, ApplicationDataMutationLedgerStatus.Finalized, affected, null, null, "Table row update committed and ledger finalized.", null, CancellationToken.None);
                return BuildLedgerResponse(reservation.Ledger, affected, ApplicationDataMutationLedgerStatus.Finalized, requestHash, auditId);
            }
            catch (Exception exception)
            {
                var persistedFailure = await PersistMutationFailureAsync(
                    ledgerService,
                    reservation.Ledger.Id,
                    context,
                    null,
                    "UPDATE",
                    ApplicationDataMutationLedgerStatus.Unknown,
                    affected,
                    exception);
                if (persistedFailure is not null)
                    throw persistedFailure;
                throw;
            }
        }

        await ledgerService.TransitionAsync(reservation.Ledger.Id, ApplicationDataMutationLedgerStatus.Failed, affected, "OptimisticConcurrencyConflict", "Affected row count changed during update.", "Update was not accepted because the concurrency predicate changed.", null, CancellationToken.None);
        var conflict = await BuildConflictResponseAsync(db, context, provider, request.Values, request.KeyValues, request.AuditId, cancellationToken);
        return AttachLedger(conflict, reservation.Ledger, affected, requestHash);
    }

    public async Task<ApplicationDataSourceTableRowMutationResponse> DeleteRowAsync(
        string dataSourceId,
        string tableName,
        ApplicationDataSourceTableRowDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = await BuildContextAsync(dataSourceId, tableName, cancellationToken);
        EnsureWritableTable(context);
        EnsurePrimaryKeys(context);
        var parameters = new List<SugarParameter>();
        var keyWhere = BuildPrimaryKeyWhere(context, request.KeyValues, parameters, "@k");
        var provider = ResolveProvider(context.DataSource.ObjectType);
        var concurrency = BuildConcurrencyWhere(context, provider, request.VersionValue, request.OriginalValues, request.ConflictResolution, parameters);
        var concurrencyWhere = CombineWhere(keyWhere, concurrency.Sql);
        using var db = await connectionFactory.CreateDatabaseClientAsync(context.DataSource, cancellationToken);
        db.Ado.CommandTimeOut = 30;
        var expected = await EnsureImpactConfirmedAsync(db, context, provider, concurrencyWhere, parameters, request.Confirmed, request.ExpectedAffectedRows, cancellationToken);
        var sql = $"DELETE FROM {context.QuotedTableName} WHERE {concurrencyWhere}";
        var requestHash = ResolveRequestHash(request.RequestHash, ComputeMutationHash("DELETE", dataSourceId, tableName, request.KeyValues, null, request.VersionValue, request.OriginalValues, request.ExpectedAffectedRows));
        var ledgerService = mutationLedgerService ?? new ApplicationDataMutationLedgerService(databaseAccessor, workspaceResolver);
        var reservation = await ledgerService.ReserveAsync(
            BuildLedgerReservation("DELETE", requestHash, context, sql, request.ExpectedAffectedRows),
            cancellationToken);
        if (!reservation.IsNew)
        {
            return BuildLedgerResponse(reservation.Ledger);
        }

        var commandStarted = false;
        int affected;
        try
        {
            commandStarted = true;
            affected = await db.Ado.ExecuteCommandAsync(sql, parameters.ToArray(), cancellationToken);
        }
        catch (Exception exception)
        {
            var status = commandStarted ? ApplicationDataMutationLedgerStatus.Unknown : ApplicationDataMutationLedgerStatus.Failed;
            var persistedFailure = await PersistMutationFailureAsync(
                ledgerService,
                reservation.Ledger.Id,
                context,
                request.AuditId,
                "DELETE",
                status,
                0,
                exception);
            if (persistedFailure is not null)
                throw persistedFailure;
            throw;
        }
        if (affected == expected)
        {
            try
            {
                var auditId = await WriteMutationAuditAsync(context, request.AuditId, "DELETE", affected, true, null, CancellationToken.None);
                await ledgerService.TransitionAsync(reservation.Ledger.Id, ApplicationDataMutationLedgerStatus.Finalized, affected, null, null, "Table row delete committed and ledger finalized.", null, CancellationToken.None);
                return BuildLedgerResponse(reservation.Ledger, affected, ApplicationDataMutationLedgerStatus.Finalized, requestHash, auditId);
            }
            catch (Exception exception)
            {
                var persistedFailure = await PersistMutationFailureAsync(
                    ledgerService,
                    reservation.Ledger.Id,
                    context,
                    null,
                    "DELETE",
                    ApplicationDataMutationLedgerStatus.Unknown,
                    affected,
                    exception);
                if (persistedFailure is not null)
                    throw persistedFailure;
                throw;
            }
        }

        await ledgerService.TransitionAsync(reservation.Ledger.Id, ApplicationDataMutationLedgerStatus.Failed, affected, "OptimisticConcurrencyConflict", "Affected row count changed during delete.", "Delete was not accepted because the concurrency predicate changed.", null, CancellationToken.None);
        var conflict = await BuildConflictResponseAsync(db, context, provider, request.OriginalValues, request.KeyValues, request.AuditId, cancellationToken);
        return AttachLedger(conflict, reservation.Ledger, affected, requestHash);
    }

    private static async Task<int> EnsureImpactConfirmedAsync(
        ISqlSugarClient db,
        ApplicationDataSourceTableRowContext context,
        IApplicationDataSourceProvider provider,
        string keyWhere,
        IReadOnlyList<SugarParameter> parameters,
        bool confirmed,
        int? expectedAffectedRows,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var count = Convert.ToInt32(await ExecuteScalarAsync(db, $"SELECT COUNT(1) FROM {context.QuotedTableName} WHERE {keyWhere}", parameters.ToArray(), cancellationToken));
        if (!confirmed || expectedAffectedRows is null)
        {
            throw new ValidationException($"写操作影响行数为 {count}，必须使用相同行数重新确认", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if ((count == 0 && expectedAffectedRows.Value != 1) || (count != 0 && expectedAffectedRows.Value != count))
        {
            throw new ValidationException("写操作影响行数与确认行数不一致，必须重新确认", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        if (count > provider.Capability.MaxWriteRows)
        {
            throw new ValidationException("单次写操作影响行数超过 1000，必须拆分条件或走审批流程", ErrorCodes.PermissionDenied);
        }
        return count == 0 ? expectedAffectedRows.Value : count;
    }

    private async Task<ApplicationDataSourceTableRowContext> BuildContextAsync(
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken)
    {
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        var dataSource = await EnsureDataSourceAsync(appDb, workspace, dataSourceId, cancellationToken);
        EnsureDatabase(dataSource);
        var (requestedSchema, _) = ParseQualifiedName(tableName);
        var provider = ResolveProvider(dataSource.ObjectType);
        if (!string.IsNullOrWhiteSpace(requestedSchema) && !provider.Capability.SupportsSchemas)
        {
            throw new ValidationException(
                $"Provider '{provider.Type}' does not support schema-qualified table access.",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        var table = await ResolveTableAsync(dataSourceId, tableName, cancellationToken);
        if (!string.IsNullOrWhiteSpace(table.SchemaName) && !provider.Capability.SupportsSchemas)
        {
            throw new ValidationException(
                $"Provider '{provider.Type}' does not support schema-qualified table access.",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
        var qualifiedName = BuildQualifiedName(table.SchemaName, table.TableName);
        var columns = await dataSourceService.GetColumnsAsync(dataSourceId, qualifiedName, cancellationToken);
        if (columns.Count == 0)
        {
            throw new ValidationException("当前表没有可读取字段", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return new ApplicationDataSourceTableRowContext(
            dataSource,
            table,
            columns,
            ApplicationDataSourceSqlNamePolicy.QuoteQualified(dataSource.ObjectType, table.SchemaName, table.TableName));
    }

    private async Task<ApplicationDataSourceTableResponse> ResolveTableAsync(
        string dataSourceId,
        string tableName,
        CancellationToken cancellationToken)
    {
        var (schemaName, normalizedTableName) = ParseQualifiedName(tableName);
        var tables = await dataSourceService.GetTablesAsync(dataSourceId, cancellationToken);
        return tables.FirstOrDefault(item =>
            string.Equals(item.TableName, normalizedTableName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(item.SchemaName ?? string.Empty, schemaName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            ?? tables.FirstOrDefault(item =>
                string.IsNullOrWhiteSpace(schemaName) &&
                string.Equals(item.TableName, normalizedTableName, StringComparison.OrdinalIgnoreCase))
            ?? throw new NotFoundException("数据表不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private static ApplicationDataSourceSqlClause BuildWhereClause(
        ApplicationDataSourceTableRowContext context,
        IApplicationDataSourceProvider provider,
        string? keyword,
        IReadOnlyList<ApplicationDataSourceTableRowFilterRequest> filters)
    {
        var clauses = new List<string>();
        var parameters = new List<SugarParameter>();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var keywordClauses = context.Columns
                .Where(IsTextLikeColumn)
                .Select(column =>
                {
                    var parameterName = $"@kw{parameters.Count}";
                    parameters.Add(new SugarParameter(parameterName, $"%{keyword.Trim()}%"));
                    return provider.BuildTextSearchSql(
                        ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, column.ColumnName),
                        parameterName);
                })
                .ToArray();
            if (keywordClauses.Length > 0)
            {
                clauses.Add($"({string.Join(" OR ", keywordClauses)})");
            }
        }

        foreach (var filter in filters.Where(item => !string.IsNullOrWhiteSpace(item.FieldCode)))
        {
            var column = context.RequireColumn(filter.FieldCode);
            var parameterName = $"@p{parameters.Count}";
            var operatorName = string.IsNullOrWhiteSpace(filter.Operator) ? "contains" : filter.Operator.Trim();
            var sqlOperator = ResolveOperator(operatorName);
            object? value = NormalizeParameterValue(filter.Value);
            if (string.Equals(operatorName, "contains", StringComparison.OrdinalIgnoreCase))
            {
                value = $"%{value}%";
            }

            parameters.Add(new SugarParameter(parameterName, value));
            clauses.Add($"{ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, column.ColumnName)} {sqlOperator} {parameterName}");
        }

        return clauses.Count == 0
            ? new ApplicationDataSourceSqlClause(string.Empty, parameters)
            : new ApplicationDataSourceSqlClause(" WHERE " + string.Join(" AND ", clauses), parameters);
    }

    private static string BuildOrderByClause(
        ApplicationDataSourceTableRowContext context,
        IReadOnlyList<ApplicationDataSourceTableRowSortRequest> sorts)
    {
        var clauses = sorts
            .Where(item => !string.IsNullOrWhiteSpace(item.FieldCode))
            .Take(5)
            .Select(item =>
            {
                var column = context.RequireColumn(item.FieldCode);
                var direction = string.Equals(item.Direction, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                return $"{ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, column.ColumnName)} {direction}";
            })
            .ToArray();
        if (clauses.Length > 0)
        {
            return " ORDER BY " + string.Join(", ", clauses);
        }

        var defaultColumn = context.PrimaryKeys.FirstOrDefault() ?? context.Columns.OrderBy(column => column.Order).First().ColumnName;
        return " ORDER BY " + ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, defaultColumn);
    }


    private static string BuildCsvContent(
        IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> fields,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows)
    {
        var builder = new StringBuilder("\uFEFF");
        builder.AppendLine(string.Join(',', fields.Select(field => EscapeCsv(field.FieldName ?? field.FieldCode))));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', fields.Select(field => EscapeCsv(row.TryGetValue(field.FieldCode, out var value) ? value : null))));
        }

        return builder.ToString();
    }

    private static string BuildExportSelectSql(ApplicationDataSourceTableRowContext context) =>
        string.Join(", ", context.Columns.Select(column =>
            ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, column.ColumnName)));

    private static string EscapeCsv(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            byte[] bytes => Convert.ToBase64String(bytes),
            DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }

    private static string SanitizeFileName(string tableName)
    {
        var value = string.Concat(tableName.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        return string.IsNullOrWhiteSpace(value) ? "data" : value;
    }

    private static ApplicationDataSourceTableRowsResponse BuildRowsResponse(
        ApplicationDataSourceTableRowContext context,
        IApplicationDataSourceProvider provider,
        DataTable dataTable,
        int total,
        int pageIndex,
        int pageSize)
    {
        var preview = ApplicationDataSourcePreviewMapper.Map(dataTable, "查询成功");
        var editable = IsWritableDataTable(context);
        return new ApplicationDataSourceTableRowsResponse
        {
            Fields = preview.Fields,
            Rows = preview.Rows,
            PrimaryKeys = context.PrimaryKeys.ToArray(),
            Total = total,
            PageIndex = pageIndex,
            PageSize = pageSize,
            Editable = editable,
            EditDisabledReason = editable ? null : ResolveEditDisabledReason(context),
            CanInsert = IsInsertableTable(context),
            InsertDisabledReason = IsInsertableTable(context) ? null : ResolveInsertDisabledReason(context)
            ,ConcurrencyStrategy = ResolveConcurrencyStrategy(context, provider)
            ,ConcurrencyColumn = ResolveVersionColumn(context, provider)
            ,ConcurrencyDisabledReason = context.PrimaryKeys.Count == 0 ? "表没有主键，无法安全写入" : null
        };
    }

    private static string BuildRowSelectSql(ApplicationDataSourceTableRowContext context, IApplicationDataSourceProvider provider)
    {
        var columns = context.Columns.Select(column => ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, column.ColumnName)).ToList();
        if (string.Equals(provider.Type, ApplicationDataSourceType.PostgreSql, StringComparison.OrdinalIgnoreCase) &&
            ResolveVersionColumn(context, provider) is null)
        {
            columns.Add("xmin AS \"__astererp_xmin\"");
        }

        return string.Join(", ", columns);
    }

    private static string ResolveConcurrencyStrategy(ApplicationDataSourceTableRowContext context, IApplicationDataSourceProvider provider) =>
        ResolveVersionColumn(context, provider) is not null ? "version" : context.PrimaryKeys.Count > 0 ? "originalValues" : "none";

    private static string? ResolveVersionColumn(ApplicationDataSourceTableRowContext context, IApplicationDataSourceProvider provider) =>
        context.Columns.FirstOrDefault(column => IsVersionColumn(column, provider))?.ColumnName
        ?? (string.Equals(provider.Type, ApplicationDataSourceType.PostgreSql, StringComparison.OrdinalIgnoreCase) ? "__astererp_xmin" : null);

    private static bool IsVersionColumn(ApplicationDataSourceColumnResponse column, IApplicationDataSourceProvider provider)
    {
        var name = column.ColumnName.Trim().ToLowerInvariant();
        var type = column.DataType.Trim().ToLowerInvariant();
        if (name is "version" or "row_version")
            return true;

        if (!provider.Capability.SupportsRowVersion)
            return false;

        return name is "rowversion" or "xmin" ||
               type is "rowversion" or "timestamp" or "xid" || type.Contains("rowversion", StringComparison.Ordinal);
    }

    private static (string Sql, string Strategy) BuildConcurrencyWhere(
        ApplicationDataSourceTableRowContext context,
        IApplicationDataSourceProvider provider,
        object? versionValue,
        IReadOnlyDictionary<string, object?> originalValues,
        string? resolution,
        List<SugarParameter> parameters)
    {
        if (string.Equals(resolution, "overwrite", StringComparison.OrdinalIgnoreCase))
        {
            return ("1 = 1", "overwrite");
        }

        var versionColumn = ResolveVersionColumn(context, provider);
        if (versionColumn is not null && versionValue is not null)
        {
            var parameterName = $"@v{parameters.Count}";
            parameters.Add(new SugarParameter(parameterName, NormalizeVersionValue(context, provider, versionColumn, versionValue)));
            var quoted = versionColumn == "__astererp_xmin"
                ? "xmin"
                : ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, versionColumn);
            return ($"{quoted} = {parameterName}", "version");
        }

        return (BuildOriginalValueWhere(context, originalValues, parameters, "@o"), "originalValues");
    }

    private static object? NormalizeVersionValue(ApplicationDataSourceTableRowContext context, IApplicationDataSourceProvider provider, string versionColumn, object value)
    {
        if (versionColumn == "__astererp_xmin")
            return Convert.ToUInt32(NormalizeParameterValue(value), CultureInfo.InvariantCulture);
        var column = context.RequireColumn(versionColumn);
        if (column.DataType.Contains("binary", StringComparison.OrdinalIgnoreCase) || column.DataType.Contains("rowversion", StringComparison.OrdinalIgnoreCase))
        {
            var normalized = NormalizeParameterValue(value);
            return normalized is byte[] bytes ? bytes : Convert.FromBase64String(Convert.ToString(normalized, CultureInfo.InvariantCulture)!);
        }

        return NormalizeTypedValue(column, value);
    }

    private async Task<ApplicationDataSourceTableRowMutationResponse> BuildConflictResponseAsync(
        ISqlSugarClient db,
        ApplicationDataSourceTableRowContext context,
        IApplicationDataSourceProvider provider,
        IReadOnlyDictionary<string, object?> localValues,
        IReadOnlyDictionary<string, object?> keyValues,
        string? auditId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var parameters = new List<SugarParameter>();
        var keyWhere = BuildPrimaryKeyWhere(context, keyValues, parameters, "@c");
        var table = await ExecuteDataTableAsync(db, $"SELECT {BuildRowSelectSql(context, provider)} FROM {context.QuotedTableName} WHERE {keyWhere}", parameters.ToArray(), cancellationToken);
        var serverValues = table.Rows.Count == 0
            ? new Dictionary<string, object?>()
            : context.Columns.ToDictionary(column => column.ColumnName, column => table.Rows[0][column.ColumnName] is DBNull ? null : table.Rows[0][column.ColumnName], StringComparer.OrdinalIgnoreCase);
        var auditResult = await WriteMutationAuditAsync(context, auditId, "CONFLICT", 0, false, "Optimistic concurrency conflict", cancellationToken);
        return new()
        {
            Conflict = true,
            AffectedRows = 0,
            ServerValues = serverValues,
            LocalValues = localValues,
            ConflictMessage = table.Rows.Count == 0 ? "行已被删除" : "行已被其他操作修改",
            CanRetry = table.Rows.Count > 0,
            CanOverwrite = table.Rows.Count > 0,
            AuditId = auditResult
        };
    }

    private async Task<string?> WriteMutationAuditAsync(
        ApplicationDataSourceTableRowContext context,
        string? requestedAuditId,
        string operation,
        int affectedRows,
        bool success,
        string? errorMessage,
        CancellationToken cancellationToken,
        string? outcome = null,
        string? failureCode = null,
        string? riskSummary = null)
    {
        if (auditWriter is null)
            return requestedAuditId;

        var auditId = string.IsNullOrWhiteSpace(requestedAuditId) ? Guid.NewGuid().ToString("N") : requestedAuditId.Trim();
        var statement = $"{operation} {context.Table.SchemaName}.{context.Table.TableName}";
        await auditWriter.WriteAsync(new ApplicationSqlScriptAuditEntity
        {
                Id = auditId,
                TraceId = auditId,
                SourceKind = "DataSourceTableRow",
                SourceId = context.DataSource.Id,
                SourceName = context.Table.TableName,
                DataSourceId = context.DataSource.Id,
                ScriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(statement))).ToLowerInvariant(),
                ScriptPreview = statement,
                StatementSummary = operation,
                RiskSummary = riskSummary ?? (success ? "row-mutation" : "optimistic-concurrency-conflict"),
                AffectedRows = affectedRows,
                ErrorMessage = errorMessage,
                IsSuccess = success,
                Operation = $"data.{operation.ToLowerInvariant()}",
                ResourceKind = "table.row",
                Outcome = outcome ?? (success ? "Succeeded" : "Conflict"),
                FailureCode = success ? null : failureCode ?? "OptimisticConcurrencyConflict",
                Provider = context.DataSource.ObjectType,
                TimeoutMs = 30_000,
                CancellationRequested = false,
                RequestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(statement))).ToLowerInvariant(),
                RedactedDetailsJson = $"{{\"affectedRows\":{affectedRows}}}"
            }, cancellationToken);

        return auditId;
    }

    private async Task<Exception?> PersistMutationFailureAsync(
        ApplicationDataMutationLedgerService ledgerService,
        string ledgerId,
        ApplicationDataSourceTableRowContext context,
        string? requestedAuditId,
        string operation,
        string status,
        int affectedRows,
        Exception exception)
    {
        var errors = new List<Exception>();
        var unknown = status is ApplicationDataMutationLedgerStatus.Unknown or ApplicationDataMutationLedgerStatus.RecoveryRequired;
        try
        {
            await ledgerService.TransitionAsync(
                ledgerId,
                status,
                affectedRows,
                unknown ? "ExternalWriteUnknown" : "ExternalWriteFailed",
                exception.Message,
                unknown
                    ? "External write completed or may have completed, but application audit/finalization failed; manual recovery is required."
                    : $"{operation} failed before external execution.",
                null,
                CancellationToken.None);
        }
        catch (Exception ledgerException)
        {
            errors.Add(ledgerException);
        }

        try
        {
            await WriteMutationAuditAsync(
                context,
                unknown ? null : requestedAuditId,
                operation,
                affectedRows,
                false,
                exception.Message,
                CancellationToken.None,
                unknown ? "Unknown" : "Failed",
                unknown ? "ExternalWriteUnknown" : "ExternalWriteFailed",
                unknown ? "external-write-recovery-required" : "external-write-failed");
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

    private static Dictionary<string, object?> NormalizeValues(
        ApplicationDataSourceTableRowContext context,
        IReadOnlyDictionary<string, object?> values,
        bool requireNonEmpty)
    {
        var normalized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (fieldCode, value) in values)
        {
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                continue;
            }

            var column = context.RequireColumn(fieldCode);
            normalized[column.ColumnName] = NormalizeTypedValue(column, value);
        }

        if (requireNonEmpty && normalized.Count == 0)
        {
            throw new ValidationException("行数据不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return normalized;
    }

    private static string BuildPrimaryKeyWhere(
        ApplicationDataSourceTableRowContext context,
        IReadOnlyDictionary<string, object?> keyValues,
        List<SugarParameter> parameters,
        string parameterPrefix)
    {
        var clauses = new List<string>();
        foreach (var primaryKey in context.PrimaryKeys)
        {
            if (!TryGetValue(keyValues, primaryKey, out var keyValue))
            {
                throw new ValidationException($"缺少主键字段：{primaryKey}", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var parameterName = $"{parameterPrefix}{parameters.Count}";
            parameters.Add(new SugarParameter(parameterName, NormalizeTypedValue(context.RequireColumn(primaryKey), keyValue)));
            clauses.Add($"{ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, primaryKey)} = {parameterName}");
        }

        return string.Join(" AND ", clauses);
    }

    private static string ResolveOperator(string operatorName) =>
        operatorName.Trim().ToLowerInvariant() switch
        {
            "equals" or "eq" => "=",
            "notequals" or "not-equals" or "ne" => "<>",
            "gt" => ">",
            "gte" => ">=",
            "lt" => "<",
            "lte" => "<=",
            "contains" => "LIKE",
            _ => throw new ValidationException("筛选操作符不支持", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };

    private IApplicationDataSourceProvider ResolveProvider(string sourceType) => providerRegistry.Resolve(sourceType);

    private static ApplicationDataMutationLedgerReservation BuildLedgerReservation(
        string operation,
        string requestHash,
        ApplicationDataSourceTableRowContext context,
        string sql,
        int? expectedAffectedRows) => new(
            operation,
            requestHash,
            "table.row",
            context.Table.TableName,
            context.DataSource.Id,
            context.Table.TableName,
            operation,
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql))).ToLowerInvariant(),
            context.DataSource.ObjectType,
            expectedAffectedRows);

    private static string ComputeMutationHash(
        string operation,
        string dataSourceId,
        string tableName,
        IReadOnlyDictionary<string, object?>? keyValues,
        IReadOnlyDictionary<string, object?>? values,
        object? versionValue,
        IReadOnlyDictionary<string, object?>? originalValues,
        int? expectedAffectedRows) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            operation,
            dataSourceId,
            tableName,
            keyValues,
            values,
            versionValue,
            originalValues,
            expectedAffectedRows
        })))).ToLowerInvariant();

    private static string ResolveRequestHash(string? supplied, string computed)
    {
        if (!string.IsNullOrWhiteSpace(supplied) && !string.Equals(supplied.Trim(), computed, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("RequestHash does not match the mutation request.", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return computed;
    }

    private static ApplicationDataSourceTableRowMutationResponse BuildLedgerResponse(
        ApplicationDataMutationLedgerEntity ledger,
        int? affectedRows = null,
        string? status = null,
        string? requestHash = null,
        string? auditId = null) => new()
    {
        Succeeded = string.Equals(status ?? ledger.Status, ApplicationDataMutationLedgerStatus.Finalized, StringComparison.Ordinal),
        AffectedRows = affectedRows ?? ledger.AffectedRows,
        AuditId = auditId ?? ledger.Id,
        LedgerId = ledger.Id,
        RequestHash = requestHash ?? ledger.RequestHash,
        ExecutionStatus = status ?? ledger.Status,
        RecoveryRequired = (status ?? ledger.Status) is ApplicationDataMutationLedgerStatus.Unknown or ApplicationDataMutationLedgerStatus.RecoveryRequired
    };

    private static ApplicationDataSourceTableRowMutationResponse AttachLedger(
        ApplicationDataSourceTableRowMutationResponse response,
        ApplicationDataMutationLedgerEntity ledger,
        int affectedRows,
        string requestHash) => new()
    {
        Succeeded = response.Succeeded,
        AffectedRows = response.AffectedRows == 0 ? affectedRows : response.AffectedRows,
        Conflict = response.Conflict,
        ServerValues = response.ServerValues,
        LocalValues = response.LocalValues,
        ConflictMessage = response.ConflictMessage,
        CanRetry = response.CanRetry,
        CanOverwrite = response.CanOverwrite,
        AuditId = response.AuditId,
        LedgerId = ledger.Id,
        RequestHash = requestHash,
        ExecutionStatus = ledger.Status,
        RecoveryRequired = ledger.Status is ApplicationDataMutationLedgerStatus.Unknown or ApplicationDataMutationLedgerStatus.RecoveryRequired
    };

    private static string BuildOriginalValueWhere(
        ApplicationDataSourceTableRowContext context,
        IReadOnlyDictionary<string, object?> originalValues,
        List<SugarParameter> parameters,
        string parameterPrefix)
    {
        if (originalValues.Count == 0)
        {
            throw new ValidationException("更新或删除必须携带原值条件，无法安全判断并发冲突", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var clauses = new List<string>();
        foreach (var (fieldCode, value) in originalValues)
        {
            var column = context.RequireColumn(fieldCode);
            var quoted = ApplicationDataSourceSqlNamePolicy.Quote(context.DataSource.ObjectType, column.ColumnName);
            if (value is null)
            {
                clauses.Add($"{quoted} IS NULL");
                continue;
            }

            var parameterName = $"{parameterPrefix}{parameters.Count}";
            parameters.Add(new SugarParameter(parameterName, NormalizeTypedValue(column, value)));
            clauses.Add($"{quoted} = {parameterName}");
        }

        if (clauses.Count == 0)
        {
            throw new ValidationException("原值条件不能为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return string.Join(" AND ", clauses);
    }

    private static string CombineWhere(string keyWhere, string originalWhere) =>
        $"({keyWhere}) AND ({originalWhere})";

    private static bool TryGetValue(IReadOnlyDictionary<string, object?> values, string key, out object? value)
    {
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        value = null;
        return false;
    }

    private static object? NormalizeParameterValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => element.ToString()
            };
        }

        return value;
    }

    private static object? NormalizeTypedValue(ApplicationDataSourceColumnResponse column, object? value)
    {
        value = NormalizeParameterValue(value);
        if (value is null)
        {
            if (!column.Nullable || column.PrimaryKey)
            {
                throw new ValidationException($"字段 {column.ColumnName} 不允许为空", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            return null;
        }

        var dataType = column.DataType.Trim().ToLowerInvariant();
        try
        {
            if (dataType.Contains("bool", StringComparison.Ordinal) || dataType is "bit")
            {
                return value switch
                {
                    bool boolean => boolean,
                    string text when bool.TryParse(text, out var parsed) => parsed,
                    JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
                    _ => Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0
                };
            }

            if (dataType.Contains("bigint", StringComparison.Ordinal) || dataType is "long" or "int8" or "bigserial")
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            if (dataType.Contains("int", StringComparison.Ordinal) || dataType is "serial" or "smallserial")
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (dataType.Contains("decimal", StringComparison.Ordinal) || dataType.Contains("numeric", StringComparison.Ordinal) ||
                dataType.Contains("money", StringComparison.Ordinal))
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (dataType.Contains("real", StringComparison.Ordinal) || dataType.Contains("float", StringComparison.Ordinal) ||
                dataType.Contains("double", StringComparison.Ordinal))
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (dataType.Contains("date", StringComparison.Ordinal) || dataType.Contains("time", StringComparison.Ordinal))
                return value is DateTime or DateTimeOffset
                    ? value
                    : DateTime.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            throw new ValidationException($"字段 {column.ColumnName} 的值与 {column.DataType} 类型不兼容", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return value;
    }

    private static bool IsTextLikeColumn(ApplicationDataSourceColumnResponse column)
    {
        var type = column.DataType.Trim();
        return type.Contains("char", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("text", StringComparison.OrdinalIgnoreCase) ||
               type.Contains("clob", StringComparison.OrdinalIgnoreCase) ||
               type.Equals("string", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildTextCastExpression(string sourceType, string columnName)
    {
        var quoted = ApplicationDataSourceSqlNamePolicy.Quote(sourceType, columnName);
        return sourceType switch
        {
            ApplicationDataSourceType.SqlServer => $"CAST({quoted} AS NVARCHAR(MAX))",
            ApplicationDataSourceType.MySql => $"CAST({quoted} AS CHAR)",
            _ => $"CAST({quoted} AS TEXT)"
        };
    }

    private static void EnsureWritableTable(ApplicationDataSourceTableRowContext context)
    {
        EnsureDatabase(context.DataSource);
        if (!IsWritableDataTable(context))
        {
            throw new ValidationException(ResolveEditDisabledReason(context) ?? "当前表不可编辑", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void EnsureInsertableTable(ApplicationDataSourceTableRowContext context)
    {
        EnsureDatabase(context.DataSource);
        if (!IsInsertableTable(context))
        {
            throw new ValidationException(ResolveInsertDisabledReason(context) ?? "当前表不可新增", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void EnsurePrimaryKeys(ApplicationDataSourceTableRowContext context)
    {
        if (context.PrimaryKeys.Count == 0)
        {
            throw new ValidationException("当前表没有主键，只允许查询和新增", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static bool IsWritableDataTable(ApplicationDataSourceTableRowContext context) =>
        !context.DataSource.IsReadOnly &&
        !IsView(context.Table.TableType) &&
        context.PrimaryKeys.Count > 0;

    private static bool IsInsertableTable(ApplicationDataSourceTableRowContext context) =>
        !context.DataSource.IsReadOnly &&
        !IsView(context.Table.TableType);

    private static string? ResolveEditDisabledReason(ApplicationDataSourceTableRowContext context)
    {
        if (context.DataSource.IsReadOnly)
        {
            return "当前数据源为只读，不能编辑数据";
        }

        if (IsView(context.Table.TableType))
        {
            return "视图只允许查询，不能在数据表管理中编辑";
        }

        return context.PrimaryKeys.Count == 0 ? "当前表没有主键，只允许查询和新增" : null;
    }

    private static string? ResolveInsertDisabledReason(ApplicationDataSourceTableRowContext context)
    {
        if (context.DataSource.IsReadOnly)
        {
            return "当前数据源为只读，不能新增数据";
        }

        return IsView(context.Table.TableType) ? "视图只允许查询，不能新增数据" : null;
    }

    private static bool IsView(string tableType) =>
        tableType.Contains("view", StringComparison.OrdinalIgnoreCase);

    private static void EnsureDatabase(ApplicationDataSourceEntity dataSource)
    {
        if (!ApplicationDataSourceConnectionFactory.IsDatabaseType(dataSource.ObjectType))
        {
            throw new ValidationException("当前数据源不是数据库", ErrorCodes.ApplicationDataCenterInvalidConfig);
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

    private static string BuildQualifiedName(string? schemaName, string tableName) =>
        string.IsNullOrWhiteSpace(schemaName) ? tableName : $"{schemaName}.{tableName}";

}
