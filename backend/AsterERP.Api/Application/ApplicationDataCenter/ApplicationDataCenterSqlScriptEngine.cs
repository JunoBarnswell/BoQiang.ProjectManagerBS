using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed partial class ApplicationDataCenterSqlScriptEngine(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ApplicationDataCenterWorkspaceResolver workspaceResolver,
    RuntimeValueExpressionEvaluator expressionEvaluator,
    ApplicationDataSourceConnectionFactory connectionFactory,
    ApplicationDataCenterSqlBuiltInVariableProvider builtInVariableProvider,
    ApplicationDataCenterSqlScriptParser parser,
    ApplicationDataCenterSqlScriptValidator validator,
    ApplicationDataCenterSqlScriptExpressionEvaluator scriptExpressionEvaluator,
    ApplicationDataCenterSqlScriptFunctionParameterizer functionParameterizer,
    ApplicationDataCenterSqlScriptIfElseBlockReader ifElseBlockReader,
    ApplicationDataCenterSqlScriptResultProjector resultProjector,
    ApplicationDataCenterSqlScriptAuditWriter auditWriter,
    IHttpContextAccessor httpContextAccessor,
    ILogger<ApplicationDataCenterSqlScriptEngine> logger)
{
    private const int DefaultMaxRows = 200;
    private const int MaxReturnRows = 500;
    private const int DefaultTimeoutMs = 30_000;

    public async Task<ApplicationDataCenterSqlScriptExecutionResult> ExecuteAsync(
        ApplicationDataCenterSqlScriptExecutionRequest request,
        CancellationToken cancellationToken)
    {
        var traceId = ResolveTraceId(request);
        var startedAt = Stopwatch.GetTimestamp();
        var audit = CreateAudit(request, traceId);
        ApplicationDataCenterSqlScriptPlan? plan = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultTimeoutMs);
        var executionToken = timeout.Token;

        try
        {
            var validationErrors = new List<string>();
            validator.ValidateSqlScript(request.SqlScript, validationErrors, request.SourceName ?? request.SourceKind);
            if (validationErrors.Count > 0)
            {
                throw new ValidationException(string.Join("；", validationErrors), ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var scriptVariables = BuildScriptVariables(request);
            var controlResolvedScript = ExecuteControlStatements(request.SqlScript.Script, scriptVariables);
            RejectUserTransactionStatements(controlResolvedScript);
            ApplyBuiltInVariables(scriptVariables);
            var parameterizedScript = functionParameterizer.Parameterize(controlResolvedScript, scriptVariables);
            var executableScript = parameterizedScript.Script;
            plan = parser.Parse(executableScript);
            audit.StatementSummary = BuildStatementSummary(plan);
            audit.RiskSummary = BuildRiskSummary(plan);

            var parameters = BuildSqlParameters(executableScript, scriptVariables);
            audit.ParameterSummaryJson = JsonSerializer.Serialize(parameters.Select(parameter => new
            {
                name = parameter.ParameterName,
                isNull = parameter.Value is null or DBNull
            }), ApplicationDataCenterJson.Options);

            if (plan.SqlStatements.Count > 0)
            {
                await auditWriter.EnsureAvailableAsync(CancellationToken.None);
            }

            var rows = await ExecutePlanAsync(request, executableScript, plan, scriptVariables, parameters, audit, executionToken);
            rows = rows.Take(ResolveMaxRows(request.SqlScript)).ToArray();
            audit.IsSuccess = true;
            audit.ReturnedRows = rows.Count;
            audit.DurationMs = (long)Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            audit.Outcome = "Succeeded";
            audit.TimeoutMs = DefaultTimeoutMs;
            var auditSummary = await auditWriter.WriteAsync(audit, CancellationToken.None);
            var message = rows.Count == 0
                ? "SQL 执行成功，未返回数据"
                : $"SQL 执行成功，返回 {rows.Count} 行";
            var preview = resultProjector.BuildPreview(
                rows,
                request.PageIndex.GetValueOrDefault(1),
                request.PageSize.GetValueOrDefault(50),
                message,
                auditSummary);

            return new ApplicationDataCenterSqlScriptExecutionResult(rows, preview, auditSummary);
        }
        catch (Exception exception)
        {
            audit.IsSuccess = false;
            audit.ErrorMessage = exception.Message;
            audit.CancellationRequested = executionToken.IsCancellationRequested;
            audit.Outcome = exception is OperationCanceledException
                ? cancellationToken.IsCancellationRequested ? "Canceled" : "TimedOut"
                : "Failed";
            audit.FailureCode = exception is BusinessException businessException
                ? businessException.Code.ToString(CultureInfo.InvariantCulture)
                : exception.GetType().Name;
            audit.TimeoutMs = DefaultTimeoutMs;
            audit.DurationMs = (long)Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            audit.StatementSummary = string.IsNullOrWhiteSpace(audit.StatementSummary) && plan is not null
                ? BuildStatementSummary(plan)
                : string.IsNullOrWhiteSpace(audit.StatementSummary) ? "ParseFailed" : audit.StatementSummary;
            // Persist the outcome even when the request token was cancelled. The database operation
            // already observed the caller token; audit persistence is a separate safety obligation.
            await auditWriter.WriteAsync(audit, CancellationToken.None);
            logger.LogWarning(
                exception,
                "SQL script execution failed. SourceKind={SourceKind} SourceId={SourceId} TraceId={TraceId}",
                request.SourceKind,
                request.SourceId,
                traceId);
            throw;
        }
    }

    private async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ExecutePlanAsync(
        ApplicationDataCenterSqlScriptExecutionRequest request,
        string executableScript,
        ApplicationDataCenterSqlScriptPlan plan,
        Dictionary<string, object?> scriptVariables,
        SugarParameter[] parameters,
        ApplicationSqlScriptAuditEntity audit,
        CancellationToken cancellationToken)
    {
        var requiresDatabase = plan.SqlStatements.Count > 0 ||
            string.Equals(plan.ReturnKind, "select", StringComparison.OrdinalIgnoreCase);
        ISqlSugarClient? db = null;
        ApplicationDataSourceEntity? dataSource = null;
        if (requiresDatabase)
        {
            dataSource = await ResolveDataSourceAsync(request, audit, cancellationToken);
            db = await connectionFactory.CreateDatabaseClientAsync(dataSource, cancellationToken);
            db.Ado.CommandTimeOut = DefaultTimeoutMs / 1000;
            audit.Provider = dataSource.ObjectType;
            await db.Ado.BeginTranAsync();
        }

        try
        {
            if (db is not null)
            {
                audit.AffectedRows = await ExecuteStatementsAsync(db, dataSource!, plan.SqlStatements, parameters, cancellationToken);
            }

            if (string.Equals(plan.ReturnKind, "variable", StringComparison.OrdinalIgnoreCase))
            {
                var result = NormalizeReturnVariable(scriptVariables.TryGetValue(plan.ReturnVariableName, out var value) ? value : null);
                await CommitTransactionAsync(db);
                return result;
            }

            if (string.Equals(plan.ReturnKind, "json", StringComparison.OrdinalIgnoreCase))
            {
                var result = NormalizeReturnVariable(EvaluateJsonReturn(plan.ReturnJson, scriptVariables));
                await CommitTransactionAsync(db);
                return result;
            }

            if (db is null)
            {
                throw new ValidationException("RETURN SELECT 缺少当前数据源上下文", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var rows = await ReadRowsAsync(db, plan.ReturnSql, parameters, cancellationToken);
            await CommitTransactionAsync(db);
            return rows;
        }
        catch
        {
            await RollbackTransactionAsync(db);
            throw;
        }
        finally
        {
            db?.Dispose();
        }
    }

    private async Task<ApplicationDataSourceEntity> ResolveDataSourceAsync(
        ApplicationDataCenterSqlScriptExecutionRequest request,
        ApplicationSqlScriptAuditEntity audit,
        CancellationToken cancellationToken)
    {
        var dataSourceId = ResolveDataSourceId(request.SqlScript, request.ContextDataSourceId);
        audit.DataSourceId = dataSourceId;
        var workspace = workspaceResolver.Resolve();
        var appDb = await databaseAccessor.RequireApplicationDbAsync(cancellationToken);
        return (await appDb.Queryable<ApplicationDataSourceEntity>()
            .Where(item => item.Id == dataSourceId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault()
            ?? throw new NotFoundException("SQL 脚本数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
    }

    private static async Task<int> ExecuteStatementsAsync(ISqlSugarClient db, ApplicationDataSourceEntity dataSource, IReadOnlyList<string> statements, SugarParameter[] parameters, CancellationToken cancellationToken)
    {
        if (statements.Count == 0)
        {
            return 0;
        }

        var affectedRows = 0;
        foreach (var statement in statements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            affectedRows += Math.Max(0, await db.Ado.ExecuteCommandAsync(RewriteStatement(statement, dataSource), parameters, cancellationToken));
        }

        return affectedRows;
    }

    private Dictionary<string, object?> BuildScriptVariables(ApplicationDataCenterSqlScriptExecutionRequest request)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (request.ExpressionContext.Sources.TryGetValue("variables", out var variablesSource) &&
            variablesSource is IEnumerable<KeyValuePair<string, object?>> variables)
        {
            foreach (var variable in variables)
            {
                values[ApplicationDataCenterSqlScriptValidator.NormalizeName(variable.Key)] = ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(variable.Value);
            }
        }

        foreach (var parameter in request.SqlScript.Parameters.Select((item, index) => new { Item = item, Index = index }))
        {
            var name = ApplicationDataCenterSqlScriptValidator.NormalizeName(parameter.Item.Name);
            values[name] = parameter.Item.Expression is null
                ? null
                : ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(expressionEvaluator.Evaluate(
                    parameter.Item.Expression,
                    request.ExpressionContext,
                    CreateDescriptor(request, $"sqlScript.parameters[{parameter.Index}].expression", name)));
        }

        foreach (var variable in request.SqlScript.LocalVariables.Select((item, index) => new { Item = item, Index = index }))
        {
            var name = ApplicationDataCenterSqlScriptValidator.NormalizeName(variable.Item.Name);
            values[name] = variable.Item.Initializer is null
                ? null
                : ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(expressionEvaluator.Evaluate(
                    variable.Item.Initializer,
                    request.ExpressionContext,
                    CreateDescriptor(request, $"sqlScript.localVariables[{variable.Index}].initializer", name)));
        }

        foreach (var name in parser.ExtractDeclaredVariableNames(request.SqlScript.Script))
        {
            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(name))
            {
                throw new ValidationException(
                    $"SQL 脚本不能声明内置变量 @{name}",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            values.TryAdd(name, null);
        }

        ApplyBuiltInVariables(values);
        return values;
    }

    private void ApplyBuiltInVariables(Dictionary<string, object?> variables)
    {
        foreach (var item in builtInVariableProvider.Build())
        {
            variables[item.Key] = item.Value;
        }
    }

    private string ExecuteControlStatements(string script, Dictionary<string, object?> variables)
    {
        var resolved = ResolveIfElse(script, variables);
        resolved = ExecuteForBlocks(resolved, variables);
        resolved = ExecuteSetStatements(resolved, variables);
        resolved = RemoveDeclareStatements(resolved);
        return resolved.Trim();
    }

    private string ResolveIfElse(string script, Dictionary<string, object?> variables)
    {
        var resolved = script;
        for (var guard = 0; guard < 20; guard += 1)
        {
            if (!ifElseBlockReader.TryReadFirst(resolved, out var block))
            {
                return resolved;
            }

            var condition = Convert.ToBoolean(scriptExpressionEvaluator.Evaluate(block.Condition, variables), CultureInfo.InvariantCulture);
            var selectedBlock = condition ? block.ThenBlock : block.ElseBlock;
            resolved = resolved.Remove(block.Index, block.Length).Insert(block.Index, selectedBlock);
        }

        throw new ValidationException("SQL 脚本 IF/ELSE 嵌套过深", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private string ExecuteForBlocks(string script, Dictionary<string, object?> variables)
    {
        var resolved = script;
        for (var guard = 0; guard < 20; guard += 1)
        {
            var match = ForRegex().Match(resolved);
            if (!match.Success)
            {
                return resolved;
            }

            var itemName = match.Groups["item"].Value;
            var listName = match.Groups["list"].Value;
            var body = match.Groups["body"].Value;
            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(itemName))
            {
                throw new ValidationException(
                    $"SQL 脚本不能使用内置变量 @{itemName} 作为 FOR 循环变量",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (variables.TryGetValue(listName, out var listValue) && TryReadEnumerable(listValue, out var items))
            {
                var index = 0;
                foreach (var item in items)
                {
                    variables[itemName] = item;
                    variables["index"] = index;
                    ExecuteSetStatements(body, variables);
                    index += 1;
                }
            }

            resolved = resolved.Remove(match.Index, match.Length);
        }

        throw new ValidationException("SQL 脚本 FOR 嵌套过深", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private string ExecuteSetStatements(string script, Dictionary<string, object?> variables)
    {
        return SetRegex().Replace(script, match =>
        {
            var name = match.Groups["name"].Value;
            if (ApplicationDataCenterSqlBuiltInVariableNames.IsReserved(name))
            {
                throw new ValidationException(
                    $"SQL 脚本不能给内置变量 @{name} 赋值",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            variables[name] = scriptExpressionEvaluator.Evaluate(match.Groups["expression"].Value, variables);
            return string.Empty;
        });
    }

    private static object? EvaluateJsonReturn(string expression, IReadOnlyDictionary<string, object?> variables)
    {
        var normalized = expression.Trim();
        foreach (var item in variables)
        {
            normalized = Regex.Replace(
                normalized,
                $@"@{Regex.Escape(item.Key)}\b",
                JsonSerializer.Serialize(ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(item.Value)),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return JsonSerializer.Deserialize<object?>(normalized);
    }

    private static SugarParameter[] BuildSqlParameters(
        string sql,
        IReadOnlyDictionary<string, object?> variables)
    {
        return ExtractParameterNames(sql)
            .Select(name => new SugarParameter($"@{name}", ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(variables.TryGetValue(name, out var value) ? value : null) ?? DBNull.Value))
            .ToArray();
    }

    private static IReadOnlySet<string> ExtractParameterNames(string sql)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ParameterRegex().Matches(sql))
        {
            names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> NormalizeReturnVariable(object? value)
    {
        if (value is null || value is string)
        {
            return [new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["value"] = ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(value) }];
        }

        if (TryReadEnumerable(value, out var items))
        {
            return items.Select(ApplicationDataCenterSqlScriptResultProjector.NormalizeRow).ToArray();
        }

        return [ApplicationDataCenterSqlScriptResultProjector.NormalizeRow(value)];
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ReadRows(DataTable table)
    {
        return table.Rows
            .Cast<DataRow>()
            .Select(row =>
            {
                var item = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (DataColumn column in table.Columns)
                {
                    var value = row[column];
                    item[column.ColumnName] = value == DBNull.Value ? null : ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(value);
                }

                return (IReadOnlyDictionary<string, object?>)item;
            })
            .ToArray();
    }

    private static async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> ReadRowsAsync(
        ISqlSugarClient db,
        string sql,
        SugarParameter[] parameters,
        CancellationToken cancellationToken)
    {
        var connection = db.Ado.Connection as DbConnection
            ?? throw new InvalidOperationException("当前数据源不支持异步结果读取");
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
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < reader.FieldCount; index += 1)
            {
                var value = reader.GetValue(index);
                row[reader.GetName(index)] = value == DBNull.Value
                    ? null
                    : ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(value);
            }

            rows.Add(row);
        }

        return rows;
    }

    private static ApplicationSqlScriptAuditEntity CreateAudit(ApplicationDataCenterSqlScriptExecutionRequest request, string traceId)
    {
        return new ApplicationSqlScriptAuditEntity
        {
            TraceId = traceId,
            SourceKind = request.SourceKind,
            SourceId = request.SourceId,
            SourceName = request.SourceName,
            ScriptHash = HashScript(request.SqlScript.Script),
            ScriptPreview = CreateScriptPreview(request.SqlScript.Script),
            StatementSummary = "Pending",
            RiskSummary = "Pending",
            ParameterSummaryJson = "[]"
            ,Operation = "sql.execute"
            ,ResourceKind = "sql"
            ,Outcome = "Pending"
            ,RequestHash = HashScript(request.SqlScript.Script)
            ,RedactedDetailsJson = "{}"
        };
    }

    private static string BuildStatementSummary(ApplicationDataCenterSqlScriptPlan plan)
    {
        var statements = plan.SqlStatements
            .Select(ResolveStatementKind)
            .Append($"RETURN {plan.ReturnKind.ToUpperInvariant()}")
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}:{group.Count()}");
        return string.Join(", ", statements);
    }

    private static string BuildRiskSummary(ApplicationDataCenterSqlScriptPlan plan)
    {
        var inspect = ApplicationDataCenterSqlScriptParser.RemoveStringLiteralsAndComments(plan.OriginalScript);
        var risks = new List<string>();
        foreach (var keyword in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE", "CREATE TEMP TABLE", "DROP TEMP TABLE", "DROP TABLE IF EXISTS temp." })
        {
            if (inspect.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                risks.Add(keyword);
            }
        }

        return risks.Count == 0 ? "ReadOnly" : string.Join(", ", risks);
    }

    private static string ResolveStatementKind(string statement)
    {
        var match = Regex.Match(statement.Trim(), @"^[A-Za-z]+", RegexOptions.CultureInvariant);
        return match.Success ? match.Value.ToUpperInvariant() : "SQL";
    }

    private static string ResolveDataSourceId(ApplicationMicroflowSqlScriptDefinition sqlScript, string? contextDataSourceId)
    {
        var dataSourceId = string.IsNullOrWhiteSpace(sqlScript.DataSourceId)
            ? contextDataSourceId
            : sqlScript.DataSourceId;
        if (string.IsNullOrWhiteSpace(dataSourceId))
        {
            throw new ValidationException("SQL 脚本缺少当前数据源上下文", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return dataSourceId.Trim();
    }

    private static string RewriteStatement(string statement, ApplicationDataSourceEntity dataSource)
    {
        if (!string.Equals(dataSource.ObjectType, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            return statement;
        }

        var match = Regex.Match(statement, @"^\s*TRUNCATE\s+(?:TABLE\s+)?(?<name>[A-Za-z_][A-Za-z0-9_\.]*)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? $"DELETE FROM {match.Groups["name"].Value}" : statement;
    }

    private string ResolveTraceId(ApplicationDataCenterSqlScriptExecutionRequest request) =>
        !string.IsNullOrWhiteSpace(request.TraceId)
            ? request.TraceId!
            : httpContextAccessor.HttpContext?.TraceIdentifier ??
              Activity.Current?.Id ??
              Guid.NewGuid().ToString("N");

    private static RuntimeExpressionEvaluationDescriptor CreateDescriptor(
        ApplicationDataCenterSqlScriptExecutionRequest request,
        string expressionName,
        string bindingKey) =>
        new()
        {
            BindingKey = bindingKey,
            ExpressionName = expressionName,
            OwnerId = request.SourceId,
            OwnerName = request.SourceName,
            OwnerType = request.SourceKind
        };

    private static int ResolveMaxRows(ApplicationMicroflowSqlScriptDefinition sqlScript) =>
        Math.Clamp(sqlScript.MaxRows <= 0 ? DefaultMaxRows : sqlScript.MaxRows, 1, MaxReturnRows);

    private static string RemoveDeclareStatements(string script) =>
        DeclareRegex().Replace(script, string.Empty);

    private static bool TryReadEnumerable(object? value, out IReadOnlyList<object?> items)
    {
        value = ApplicationDataCenterSqlScriptResultProjector.NormalizeJsonLikeValue(value);
        if (value is string || value is null)
        {
            items = [];
            return false;
        }

        if (value is global::System.Collections.IEnumerable enumerable)
        {
            items = enumerable.Cast<object?>().ToArray();
            return true;
        }

        items = [];
        return false;
    }

    private static void RejectUserTransactionStatements(string script)
    {
        foreach (var statement in ApplicationDataCenterSqlScriptParser.SplitSqlStatements(
                     ApplicationDataCenterSqlScriptParser.RemoveStringLiteralsAndComments(script)))
        {
            if (Regex.IsMatch(statement, @"^\s*(BEGIN|COMMIT|ROLLBACK|SAVEPOINT|RELEASE\s+SAVEPOINT|ROLLBACK\s+TO)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new ValidationException("SQL 脚本不允许控制事务边界，事务由服务端统一管理", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }
    }

    private static Task CommitTransactionAsync(ISqlSugarClient? db) =>
        db is null ? Task.CompletedTask : db.Ado.CommitTranAsync();

    private static Task RollbackTransactionAsync(ISqlSugarClient? db) =>
        db is null ? Task.CompletedTask : db.Ado.RollbackTranAsync();

    private static string HashScript(string script)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(script));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string CreateScriptPreview(string script)
    {
        var normalized = Regex.Replace(script.Trim(), "\\s+", " ", RegexOptions.CultureInvariant);
        return normalized.Length <= 1000 ? normalized : normalized[..1000];
    }

    [GeneratedRegex("\\bDECLARE\\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s+[^;]+;?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeclareRegex();

    [GeneratedRegex("\\bSET\\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(?<expression>[^;]+);?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SetRegex();

    [GeneratedRegex("\\bFOR\\s+@(?<item>[A-Za-z_][A-Za-z0-9_]*)\\s+IN\\s+@(?<list>[A-Za-z_][A-Za-z0-9_]*)\\s*\\{(?<body>[\\s\\S]*?)\\}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForRegex();

    [GeneratedRegex("(?<!@)@(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex ParameterRegex();
}
