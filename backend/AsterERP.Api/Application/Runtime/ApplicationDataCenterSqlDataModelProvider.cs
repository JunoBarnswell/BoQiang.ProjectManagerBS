using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Runtime;

public sealed partial class ApplicationDataCenterSqlDataModelProvider(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    ApplicationDataSourceConnectionFactory connectionFactory,
    RuntimeSnowflakeIdGenerator snowflakeIdGenerator)
    : ITransactionalDataModelProvider
{
    private const int MaxPageSize = 200;
    private SqlSourceTransactionContext? activeTransaction;

    public string ProviderKey => "application-data-center.sql-table";

    public async Task<T> ExecuteInTransactionAsync<T>(
        IReadOnlyList<RuntimeDataModelDefinition> models,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (models.Count == 0)
        {
            throw new ValidationException("复合模型事务必须包含至少一个模型", ErrorCodes.RuntimeDataModelInvalid);
        }

        if (activeTransaction is not null)
        {
            await EnsureModelsUseTransactionSourceAsync(models, activeTransaction.SourceKey, cancellationToken);
            return await action();
        }

        var rootBinding = await ResolveSourceBindingAsync(models[0], createDatabase: true, cancellationToken);
        if (rootBinding.Db is null)
        {
            throw new ValidationException("复合模型事务无法打开数据源", ErrorCodes.RuntimeDataModelInvalid);
        }

        EnsureTransactionWritableSource(rootBinding, models[0]);
        await EnsureModelsUseTransactionSourceAsync(models.Skip(1).ToArray(), rootBinding.SourceKey, cancellationToken);

        rootBinding.Db.Ado.BeginTran();
        activeTransaction = new SqlSourceTransactionContext(rootBinding.Db, rootBinding.SourceKey);
        try
        {
            var result = await action();
            cancellationToken.ThrowIfCancellationRequested();
            rootBinding.Db.Ado.CommitTran();
            return result;
        }
        catch
        {
            rootBinding.Db.Ado.RollbackTran();
            throw;
        }
        finally
        {
            activeTransaction = null;
            if (rootBinding.OwnsDb)
            {
                rootBinding.Db.Dispose();
            }
        }
    }

    public async Task<RuntimeDataModelQueryResult> QueryAsync(
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var context = await OpenSourceAsync(model, cancellationToken);
        var sql = BuildQuerySql(context, model, query, out var parameters);
        var countSql = BuildCountSql(context, model, query, out var countParameters);
        var total = Convert.ToInt32(context.Db.Ado.GetScalar(countSql, countParameters.ToArray()));
        var table = context.Db.Ado.GetDataTable(sql, parameters.ToArray());
        return new RuntimeDataModelQueryResult(MapRows(table, model.Fields), total);
    }

    public async Task<IReadOnlyDictionary<string, object?>?> GetDetailAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var context = await OpenSourceAsync(model, cancellationToken);
        if (!context.HasTable)
        {
            return null;
        }

        var keyColumn = ResolveColumn(model, model.KeyField);
        var tableName = NormalizeSqlName(context.TableName, "表名");
        var sql = $"SELECT * FROM {tableName} WHERE {NormalizeSqlName(keyColumn, "主键字段")} = @id";
        SugarParameter[] parameters = [new SugarParameter("@id", id)];
        var table = context.Db.Ado.GetDataTable(sql, parameters);
        return table.Rows.Count == 0 ? null : MapRow(table.Rows[0], model.Fields);
    }

    public async Task<IReadOnlyDictionary<string, object?>?> CreateAsync(
        RuntimeDataModelDefinition model,
        IReadOnlyList<RuntimeDataModelFieldUpdate> values,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var context = await OpenSourceAsync(model, cancellationToken);
        if (!context.HasTable)
        {
            return null;
        }

        var writeValues = BuildCreateValues(model, values, out var createdId);
        if (writeValues.Count == 0)
        {
            return null;
        }

        var tableName = NormalizeSqlName(context.TableName, "表名");
        var columns = string.Join(", ", writeValues.Select(item => NormalizeSqlName(item.Column, "字段")));
        var valueTokens = string.Join(", ", writeValues.Select((_, index) => $"@p{index}"));
        var parameters = writeValues
            .Select((item, index) => new SugarParameter($"@p{index}", item.Value))
            .ToArray();
        context.Db.Ado.ExecuteCommand($"INSERT INTO {tableName} ({columns}) VALUES ({valueTokens})", parameters);
        return await GetDetailAsync(model, createdId, cancellationToken);
    }

    public async Task<bool> UpdateFieldsAsync(
        RuntimeDataModelDefinition model,
        string id,
        IReadOnlyList<RuntimeDataModelFieldUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var context = await OpenSourceAsync(model, cancellationToken);
        if (!context.HasTable || updates.Count == 0)
        {
            return false;
        }

        var writeValues = BuildUpdateValues(model, updates);
        if (writeValues.Count == 0)
        {
            return false;
        }

        var tableName = NormalizeSqlName(context.TableName, "表名");
        var keyColumn = NormalizeSqlName(ResolveColumn(model, model.KeyField), "主键字段");
        var sets = string.Join(", ", writeValues.Select((item, index) => $"{NormalizeSqlName(item.Column, "字段")} = @p{index}"));
        var parameters = writeValues
            .Select((item, index) => new SugarParameter($"@p{index}", item.Value))
            .Append(new SugarParameter("@id", id))
            .ToArray();
        var affected = context.Db.Ado.ExecuteCommand($"UPDATE {tableName} SET {sets} WHERE {keyColumn} = @id", parameters);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(
        RuntimeDataModelDefinition model,
        string id,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var context = await OpenSourceAsync(model, cancellationToken);
        if (!context.HasTable)
        {
            return false;
        }

        var tableName = NormalizeSqlName(context.TableName, "表名");
        var keyColumn = NormalizeSqlName(ResolveColumn(model, model.KeyField), "主键字段");
        SugarParameter[] parameters = [new SugarParameter("@id", id)];
        var affected = context.Db.Ado.ExecuteCommand(
            $"DELETE FROM {tableName} WHERE {keyColumn} = @id",
            parameters);
        return affected > 0;
    }

    private async Task<SqlSourceContext> OpenSourceAsync(
        RuntimeDataModelDefinition model,
        CancellationToken cancellationToken)
    {
        if (activeTransaction is not null)
        {
            var transactionBinding = await ResolveSourceBindingAsync(model, createDatabase: false, cancellationToken);
            if (!string.Equals(transactionBinding.SourceKey, activeTransaction.SourceKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("复合模型事务只能操作同一个数据源上下文", ErrorCodes.RuntimeDataModelInvalid);
            }

            return new SqlSourceContext(
                activeTransaction.Db,
                transactionBinding.DbType,
                transactionBinding.TableName,
                transactionBinding.Sql,
                ownsDb: false);
        }

        var binding = await ResolveSourceBindingAsync(model, createDatabase: true, cancellationToken);
        if (binding.Db is null)
        {
            throw new ValidationException("运行时模型数据源未打开", ErrorCodes.RuntimeDataModelInvalid);
        }

        return new SqlSourceContext(
            binding.Db,
            binding.DbType,
            binding.TableName,
            binding.Sql,
            binding.OwnsDb);
    }

    private async Task<SqlSourceBinding> ResolveSourceBindingAsync(
        RuntimeDataModelDefinition model,
        bool createDatabase,
        CancellationToken cancellationToken)
    {
        var source = model.Source ?? throw new ValidationException("运行时模型缺少来源配置", ErrorCodes.RuntimeDataModelInvalid);
        var tableName = ReadString(source, "tableName");
        var sql = ReadString(source, "sql");
        if (string.IsNullOrWhiteSpace(tableName) && string.IsNullOrWhiteSpace(sql))
        {
            throw new ValidationException("运行时模型缺少表名或查询 SQL", ErrorCodes.RuntimeDataModelInvalid);
        }

        var dataSourceId = ReadString(source, "dataSourceId");
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            return new SqlSourceBinding(appDb, SqlSugar.DbType.Sqlite, BuildApplicationSourceKey(workspace), tableName, sql, OwnsDb: false);
        }

        var dataSource = (await appDb.Queryable<ApplicationDataSourceEntity>()
            .Where(item =>
                item.Id == dataSourceId &&
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                !item.IsDeleted &&
                item.Status != ApplicationDataCenterObjectStatus.Disabled)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("模型来源数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);

        if (string.Equals(dataSource.ObjectType, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase))
        {
            return new SqlSourceBinding(appDb, SqlSugar.DbType.Sqlite, BuildApplicationSourceKey(workspace), tableName, sql, OwnsDb: false);
        }

        var dbType = ResolveDbType(dataSource.ObjectType);
        var databaseClient = createDatabase
            ? await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken)
            : null;
        return new SqlSourceBinding(
            databaseClient,
            dbType,
            $"datasource:{dataSource.Id}",
            tableName,
            sql,
            OwnsDb: createDatabase);
    }

    private async Task EnsureModelsUseTransactionSourceAsync(
        IReadOnlyList<RuntimeDataModelDefinition> models,
        string sourceKey,
        CancellationToken cancellationToken)
    {
        foreach (var model in models)
        {
            var binding = await ResolveSourceBindingAsync(model, createDatabase: false, cancellationToken);
            EnsureTransactionWritableSource(binding, model);
            if (!string.Equals(binding.SourceKey, sourceKey, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValidationException("复合模型事务只能操作同一个数据源上下文", ErrorCodes.RuntimeDataModelInvalid);
            }
        }
    }

    private static void EnsureTransactionWritableSource(
        SqlSourceBinding binding,
        RuntimeDataModelDefinition model)
    {
        if (string.IsNullOrWhiteSpace(binding.TableName))
        {
            throw new ValidationException($"模型 {model.ModelCode} 缺少可写入表名，不能参与复合写入事务", ErrorCodes.RuntimeDataModelInvalid);
        }
    }

    private static string BuildApplicationSourceKey(ApplicationDataCenterWorkspace workspace) =>
        $"application:{workspace.TenantId}:{workspace.AppCode}";
    private static string BuildQuerySql(
        SqlSourceContext context,
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        out List<SugarParameter> parameters)
    {
        var where = BuildWhereClause(model, query, out parameters);
        var orderBy = BuildOrderBy(model, query);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, MaxPageSize);
        var offset = (Math.Max(query.PageIndex, 1) - 1) * pageSize;
        parameters.Add(new SugarParameter("@offset", offset));
        parameters.Add(new SugarParameter("@pageSize", pageSize));
        var sourceSql = BuildSourceSql(context);

        if (context.DbType == SqlSugar.DbType.SqlServer)
        {
            return $"{sourceSql} {where} {orderBy} OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY";
        }

        return $"{sourceSql} {where} {orderBy} LIMIT @pageSize OFFSET @offset";
    }

    private static string BuildCountSql(
        SqlSourceContext context,
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        out List<SugarParameter> parameters)
    {
        var where = BuildWhereClause(model, query, out parameters);
        if (!string.IsNullOrWhiteSpace(context.Sql))
        {
            return $"SELECT COUNT(1) FROM ({NormalizeSelectSql(context.Sql)}) __runtime {where}";
        }

        return $"SELECT COUNT(1) FROM {NormalizeSqlName(context.TableName, "表名")} {where}";
    }

    private static string BuildSourceSql(SqlSourceContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Sql))
        {
            return $"SELECT * FROM ({NormalizeSelectSql(context.Sql)}) __runtime";
        }

        return $"SELECT * FROM {NormalizeSqlName(context.TableName, "表名")}";
    }

    private static string BuildWhereClause(
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query,
        out List<SugarParameter> parameters)
    {
        parameters = [];
        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keywordClauses = model.Fields
                .Where(item => item.Queryable && IsTextField(item))
                .Select(item => $"{NormalizeSqlName(ResolveColumn(model, item.FieldCode), "字段")} LIKE @keyword")
                .ToArray();
            if (keywordClauses.Length > 0)
            {
                clauses.Add($"({string.Join(" OR ", keywordClauses)})");
                parameters.Add(new SugarParameter("@keyword", $"%{query.Keyword.Trim()}%"));
            }
        }

        var index = 0;
        foreach (var filter in query.Filters)
        {
            var column = NormalizeSqlName(ResolveColumn(model, filter.Field.FieldCode), "字段");
            var parameterName = $"@f{index}";
            var parameterNameTo = $"@f{index}To";
            clauses.Add(filter.Operator.ToLowerInvariant() switch
            {
                "contains" => $"{column} LIKE {parameterName}",
                "startswith" => $"{column} LIKE {parameterName}",
                "endswith" => $"{column} LIKE {parameterName}",
                "notequals" => $"{column} <> {parameterName}",
                "gt" => $"{column} > {parameterName}",
                "gte" => $"{column} >= {parameterName}",
                "lt" => $"{column} < {parameterName}",
                "lte" => $"{column} <= {parameterName}",
                "between" => $"{column} BETWEEN {parameterName} AND {parameterNameTo}",
                _ => $"{column} = {parameterName}"
            });
            parameters.Add(new SugarParameter(parameterName, BuildFilterValue(filter)));
            if (string.Equals(filter.Operator, "between", StringComparison.OrdinalIgnoreCase))
            {
                parameters.Add(new SugarParameter(parameterNameTo, RuntimeDataProviderSupport.CoerceValue(filter.ValueTo, filter.Field.DataType)));
            }

            index += 1;
        }

        return clauses.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", clauses)}";
    }

    private static string BuildOrderBy(
        RuntimeDataModelDefinition model,
        RuntimeDataModelQuery query)
    {
        if (query.Sorts.Count == 0)
        {
            return $"ORDER BY {NormalizeSqlName(ResolveColumn(model, model.KeyField), "主键字段")}";
        }

        var sorts = query.Sorts.Select(item =>
            $"{NormalizeSqlName(ResolveColumn(model, item.Field.FieldCode), "字段")} {(item.Order.Equals("desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC")}");
        return $"ORDER BY {string.Join(", ", sorts)}";
    }

    private static object? BuildFilterValue(RuntimeDataModelFilter filter)
    {
        var value = RuntimeDataProviderSupport.CoerceValue(filter.Value, filter.Field.DataType);
        return filter.Operator.ToLowerInvariant() switch
        {
            "contains" => $"%{value}%",
            "startswith" => $"{value}%",
            "endswith" => $"%{value}",
            _ => value
        };
    }

    private IReadOnlyList<ColumnValue> BuildCreateValues(
        RuntimeDataModelDefinition model,
        IReadOnlyList<RuntimeDataModelFieldUpdate> values,
        out string createdId)
    {
        var result = new List<ColumnValue>();
        var keyColumn = ResolveColumn(model, model.KeyField);
        createdId = string.Empty;
        foreach (var value in values)
        {
            var column = ResolveColumn(model, value.Field.FieldCode);
            result.Add(new ColumnValue(column, value.Value));
            if (string.Equals(column, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                createdId = value.Value?.ToString() ?? string.Empty;
            }
        }

        if (string.IsNullOrWhiteSpace(createdId) && HasField(model, model.KeyField))
        {
            createdId = GenerateRuntimeKey(model);
            if (!string.IsNullOrWhiteSpace(createdId))
            {
                result.Insert(0, new ColumnValue(keyColumn, createdId));
            }
        }

        if (string.IsNullOrWhiteSpace(createdId))
        {
            throw new ValidationException("创建数据必须提供主键字段", ErrorCodes.RuntimeDataModelInvalid);
        }

        return result;
    }

    private IReadOnlyList<ColumnValue> BuildUpdateValues(
        RuntimeDataModelDefinition model,
        IReadOnlyList<RuntimeDataModelFieldUpdate> values)
    {
        var result = new List<ColumnValue>();
        var keyColumn = ResolveColumn(model, model.KeyField);
        foreach (var value in values)
        {
            var column = ResolveColumn(model, value.Field.FieldCode);
            if (string.Equals(column, keyColumn, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new ColumnValue(column, value.Value));
        }

        return result;
    }

    private string GenerateRuntimeKey(RuntimeDataModelDefinition model)
    {
        return model.IdGeneration.Trim().ToLowerInvariant() switch
        {
            RuntimeModelIdGeneration.Snowflake => snowflakeIdGenerator.NextId(),
            RuntimeModelIdGeneration.Manual => string.Empty,
            RuntimeModelIdGeneration.Guid => Guid.NewGuid().ToString("N"),
            _ => Guid.NewGuid().ToString("N")
        };
    }

    private static bool HasField(RuntimeDataModelDefinition model, string fieldCode) =>
        model.Fields.Any(item =>
            string.Equals(item.FieldCode, fieldCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Binding, fieldCode, StringComparison.OrdinalIgnoreCase));

    private static string ResolveColumn(RuntimeDataModelDefinition model, string fieldCode)
    {
        var field = model.Fields.FirstOrDefault(item =>
            string.Equals(item.FieldCode, fieldCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Binding, fieldCode, StringComparison.OrdinalIgnoreCase));
        if (field is null || string.IsNullOrWhiteSpace(field.Binding))
        {
            throw new ValidationException($"字段未配置数据库绑定: {fieldCode}", ErrorCodes.RuntimeFieldNotAllowed);
        }

        return field.Binding;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> MapRows(
        DataTable table,
        IReadOnlyList<RuntimeDataFieldDefinition> fields) =>
        table.Rows.Cast<DataRow>()
            .Select(row => MapRow(row, fields))
            .ToArray();

    private static IReadOnlyDictionary<string, object?> MapRow(
        DataRow row,
        IReadOnlyList<RuntimeDataFieldDefinition> fields)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var column = string.IsNullOrWhiteSpace(field.Binding) ? field.FieldCode : field.Binding;
            result[field.FieldCode] = row.Table.Columns.Contains(column) && row[column] != DBNull.Value ? row[column] : null;
        }

        return result;
    }

    private static string NormalizeSqlName(string? value, string displayName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || !SqlNameRegex().IsMatch(normalized))
        {
            throw new ValidationException($"{displayName}只能包含字母、数字、下划线和点", ErrorCodes.RuntimeDataModelInvalid);
        }

        return normalized;
    }

    private static string NormalizeSelectSql(string value)
    {
        var normalizedSql = value.Trim().TrimEnd(';');
        if (!normalizedSql.StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("运行时 SQL 只允许 SELECT 查询", ErrorCodes.RuntimeDataModelInvalid);
        }

        return normalizedSql;
    }

    private static bool IsTextField(RuntimeDataFieldDefinition field) =>
        field.DataType.Equals("text", StringComparison.OrdinalIgnoreCase) ||
        field.DataType.Equals("string", StringComparison.OrdinalIgnoreCase);

    private static string? ReadString(IReadOnlyDictionary<string, object?> source, string key)
    {
        if (!source.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value is JsonElement element ? element.ToString() : value.ToString();
    }

    private static SqlSugar.DbType ResolveDbType(string type)
    {
        if (string.Equals(type, ApplicationDataSourceType.MySql, StringComparison.OrdinalIgnoreCase))
        {
            return SqlSugar.DbType.MySql;
        }

        if (string.Equals(type, ApplicationDataSourceType.PostgreSql, StringComparison.OrdinalIgnoreCase))
        {
            return SqlSugar.DbType.PostgreSQL;
        }

        if (string.Equals(type, ApplicationDataSourceType.SqlServer, StringComparison.OrdinalIgnoreCase))
        {
            return SqlSugar.DbType.SqlServer;
        }

        return SqlSugar.DbType.Sqlite;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex SqlNameRegex();

    private sealed record ColumnValue(string Column, object? Value);

    private sealed record SqlSourceBinding(
        ISqlSugarClient? Db,
        SqlSugar.DbType DbType,
        string SourceKey,
        string? TableName,
        string? Sql,
        bool OwnsDb);

    private sealed record SqlSourceTransactionContext(ISqlSugarClient Db, string SourceKey);

    private sealed class SqlSourceContext(
        ISqlSugarClient db,
        SqlSugar.DbType dbType,
        string? tableName,
        string? sql,
        bool ownsDb) : IDisposable
    {
        public ISqlSugarClient Db { get; } = db;

        public SqlSugar.DbType DbType { get; } = dbType;

        public string? TableName { get; } = tableName;

        public string? Sql { get; } = sql;

        public bool HasTable => !string.IsNullOrWhiteSpace(TableName);

        public void Dispose()
        {
            if (ownsDb)
            {
                Db.Dispose();
            }
        }
    }
}
