using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationQueryPlanCompiler
{
    private static readonly HashSet<string> Operators = new(StringComparer.OrdinalIgnoreCase)
    { "eq", "ne", "gt", "gte", "lt", "lte", "contains", "startsWith", "endsWith", "isNull", "isNotNull" };

    private static readonly HashSet<string> ParameterTypes = new(StringComparer.OrdinalIgnoreCase)
    { "string", "int", "long", "decimal", "double", "bool", "dateTime", "guid", ApplicationMappingCacheParameterType.Number, ApplicationMappingCacheParameterType.Boolean, ApplicationMappingCacheParameterType.Date, ApplicationMappingCacheParameterType.Json };

    private static readonly HashSet<string> Aggregates = new(StringComparer.OrdinalIgnoreCase)
    { "count", "sum", "avg", "min", "max" };

    public CompiledPlan Compile(ApplicationQueryPlanRequest request, ApplicationQueryPlanResolvedModel model, IApplicationDataSourceProvider provider, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request is null || model is null || model.Nodes.Count is < 1 or > 20)
            throw InvalidPlan("QueryPlan requires a resolved Resource ID model.");
        if (!provider.Capability.SupportsSchemas && model.Nodes.Any(item => !string.IsNullOrWhiteSpace(item.Resource.SchemaName)))
            throw InvalidPlan($"Provider '{provider.Type}' does not support schema-qualified Resource IDs.");
        if (request.Filters.Count > 50 || request.Having.Count > 50 || request.Sorts.Count > 20 || request.Joins.Count > 20)
            throw InvalidPlan("QueryPlan predicate, join, or sort count exceeds the configured limit.");
        var parameters = BuildParameters(request.Parameters);
        var isWrite = string.Equals(request.AccessMode, ApplicationQueryPlanAccessMode.ControlledWrite, StringComparison.OrdinalIgnoreCase);
        if (!isWrite && !string.Equals(request.AccessMode, ApplicationQueryPlanAccessMode.ReadOnly, StringComparison.OrdinalIgnoreCase))
            throw InvalidPlan("Unsupported QueryPlan access mode.");
        return isWrite ? CompileWrite(request, model, provider, parameters) : CompileRead(request, model, provider, parameters);
    }

    private static CompiledPlan CompileRead(ApplicationQueryPlanRequest request, ApplicationQueryPlanResolvedModel model, IApplicationDataSourceProvider provider, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters)
    {
        if (request.Columns.Count is < 1 or > 100)
            throw InvalidPlan("QueryPlan must contain between 1 and 100 columns.");
        var sqlParameters = new List<SugarParameter>();
        var where = BuildPredicates(request.Filters, parameters, provider, sqlParameters, "WHERE", false, model);
        var having = BuildPredicates(request.Having, parameters, provider, sqlParameters, "HAVING", false, model);
        var select = string.Join(", ", request.Columns.Select(item => BuildSelection(item, model, provider)));
        var source = BuildSource(model, request.Joins, provider, parameters, sqlParameters);
        var group = request.GroupBy.Count == 0 ? string.Empty : " GROUP BY " + string.Join(", ", request.GroupBy.Select(item => ResolveField(item.NodeId, item.FieldResourceId, model, provider)));
        var order = request.Sorts.Count == 0 ? string.Empty : " ORDER BY " + string.Join(", ", request.Sorts.Select(item => $"{ResolveField(item.NodeId, item.FieldResourceId, model, provider)} {ResolveDirection(item.Direction)}"));
        var query = $"SELECT {select} FROM {source}{where}{group}{having}";
        var page = request.Page ?? new ApplicationQueryPlanPage();
        var size = Math.Clamp(page.Size <= 0 ? 20 : page.Size, 1, Math.Min(request.RowLimit <= 0 ? provider.Capability.MaxPageSize : request.RowLimit, provider.Capability.MaxPageSize));
        var index = Math.Max(page.Index, 1);
        var pageSql = provider.BuildPageSql(query, order, checked((index - 1) * size), size);
        var countSql = request.GroupBy.Count == 0 && request.Having.Count == 0 ? provider.BuildCountSql(source, where) : $"SELECT COUNT(1) FROM ({query}) AS query_count";
        return new(pageSql, countSql, sqlParameters, provider.Type, index, size, null, "SELECT");
    }

    private static CompiledPlan CompileWrite(ApplicationQueryPlanRequest request, ApplicationQueryPlanResolvedModel model, IApplicationDataSourceProvider provider, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters)
    {
        if (model.Nodes.Count != 1 || request.Nodes.Count != 1 || request.Joins.Count > 0 || request.GroupBy.Count > 0 || request.Having.Count > 0)
            throw InvalidPlan("Controlled writes require one resolved table Resource ID.");
        var node = model.Nodes[0];
        if (!string.Equals(node.Resource.Kind, ApplicationDataResourceKind.Table, StringComparison.OrdinalIgnoreCase))
            throw InvalidPlan("Controlled writes are only allowed for table Resource IDs.");
        var sqlParameters = new List<SugarParameter>();
        var where = BuildPredicates(request.Filters, parameters, provider, sqlParameters, "WHERE", true, model);
        var assignments = BuildWriteAssignments(request, node, parameters, provider, sqlParameters);
        var operation = request.WriteOperation?.Trim().ToLowerInvariant();
        if (operation is ApplicationQueryPlanWriteOperation.Update or ApplicationQueryPlanWriteOperation.Delete && where.Length == 0)
            throw InvalidPlan("Controlled update/delete requires a filter Resource ID.");
        if (operation == ApplicationQueryPlanWriteOperation.Insert && where.Length > 0)
            throw InvalidPlan("Controlled insert cannot contain filters.");
        var table = provider.QuoteQualified(node.Resource.SchemaName, node.Resource.ObjectName);
        var sql = operation switch
        {
            ApplicationQueryPlanWriteOperation.Insert when assignments.Count > 0 => $"INSERT INTO {table} ({string.Join(", ", assignments.Keys)}) VALUES ({string.Join(", ", assignments.Values)})",
            ApplicationQueryPlanWriteOperation.Update when assignments.Count > 0 => $"UPDATE {table} SET {string.Join(", ", assignments.Select(item => $"{item.Key} = {item.Value}"))}{where}",
            ApplicationQueryPlanWriteOperation.Delete => $"DELETE FROM {table}{where}",
            _ => throw InvalidPlan("Controlled write requires insert, update, or delete with Resource ID bindings.")
        };
        return new(string.Empty, string.Empty, sqlParameters, provider.Type, 1, 0, sql, operation!.ToUpperInvariant());
    }

    private static string BuildSource(ApplicationQueryPlanResolvedModel model, IReadOnlyList<ApplicationQueryPlanJoin> joins, IApplicationDataSourceProvider provider, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters, List<SugarParameter> sqlParameters)
    {
        var root = model.Nodes[0];
        var sql = BuildResourceSource(root.Resource, root.Alias, provider, parameters, sqlParameters);
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root.Id };
        foreach (var join in joins)
        {
            if (!model.ById.TryGetValue(join.LeftNodeId, out var leftNode) || !model.ById.TryGetValue(join.RightNodeId, out var rightNode))
                throw InvalidPlan("Join references an unknown node Resource ID.");
            var joinType = ResolveJoinType(join.Type);
            if (!provider.Capability.SupportedJoinTypes.Contains(joinType))
                throw InvalidPlan($"Provider '{provider.Type}' does not support {joinType.ToString().ToUpperInvariant()} JOIN.");
            var type = joinType switch
            {
                ApplicationQueryJoinType.Inner => "INNER",
                ApplicationQueryJoinType.Left => "LEFT",
                ApplicationQueryJoinType.Right => "RIGHT",
                ApplicationQueryJoinType.Full => "FULL",
                _ => throw InvalidPlan("Unsupported join type.")
            };
            var left = ResolveField(leftNode.Id, join.LeftFieldResourceId, model, provider);
            var right = ResolveField(rightNode.Id, join.RightFieldResourceId, model, provider);
            sql += $" {type} JOIN {BuildResourceSource(rightNode.Resource, rightNode.Alias, provider, parameters, sqlParameters)} ON {left} = {right}";
            if (!used.Add(rightNode.Id))
                throw InvalidPlan("A JOIN target node Resource ID can only be connected once.");
        }
        if (used.Count != model.Nodes.Count)
            throw InvalidPlan("Every non-root node Resource ID must be connected by a join.");
        return sql;
    }

    private static string BuildResourceSource(ApplicationQueryPlanResolvedResource resource, string alias, IApplicationDataSourceProvider provider, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters, List<SugarParameter> sqlParameters)
    {
        var table = provider.QuoteQualified(resource.SchemaName, resource.ObjectName);
        var result = string.Equals(resource.Kind, ApplicationDataResourceKind.MappingCache, StringComparison.OrdinalIgnoreCase)
            ? BuildMappingCacheSource(resource, table, provider, parameters, sqlParameters)
            : table;
        return string.IsNullOrWhiteSpace(alias) ? result : $"{result} AS {provider.QuoteIdentifier(alias)}";
    }

    private static string BuildMappingCacheSource(ApplicationQueryPlanResolvedResource resource, string table, IApplicationDataSourceProvider provider, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters, List<SugarParameter> sqlParameters)
    {
        var columns = string.Join(", ", resource.Fields.Select(field =>
        {
            var source = provider.QuoteIdentifier(field.SourceName);
            var target = provider.QuoteIdentifier(field.Name);
            return string.Equals(field.SourceName, field.Name, StringComparison.OrdinalIgnoreCase) ? source : $"{source} AS {target}";
        }));
        var predicates = resource.Parameters.Select(item =>
        {
            var parameter = ResolveParameter(item.ResourceId, item, parameters, provider, sqlParameters);
            return $"{provider.QuoteIdentifier(item.ColumnName)} = {parameter.ParameterName}";
        });
        var where = resource.Parameters.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", predicates);
        return $"(SELECT {columns} FROM {table}{where})";
    }

    private static string BuildSelection(ApplicationQueryPlanColumn column, ApplicationQueryPlanResolvedModel model, IApplicationDataSourceProvider provider)
    {
        var expression = ResolveField(column.NodeId, column.FieldResourceId, model, provider);
        if (!string.IsNullOrWhiteSpace(column.Aggregate))
        {
            var aggregate = column.Aggregate.Trim().ToLowerInvariant();
            if (!Aggregates.Contains(aggregate)) throw InvalidPlan($"Unsupported aggregate: {column.Aggregate}.");
            expression = column.FieldResourceId == "*" && aggregate == "count" ? "COUNT(1)" : $"{aggregate.ToUpperInvariant()}({expression})";
        }
        if (!string.IsNullOrWhiteSpace(column.Function))
            expression = column.Function.Trim().ToLowerInvariant() switch { "lower" => $"LOWER({expression})", "upper" => $"UPPER({expression})", "trim" => $"TRIM({expression})", "coalesce" => $"COALESCE({expression})", _ => throw InvalidPlan($"Unsupported provider function: {column.Function}.") };
        return string.IsNullOrWhiteSpace(column.Alias) ? expression : $"{expression} AS {provider.QuoteIdentifier(RequireIdentifier(column.Alias, "column alias"))}";
    }

    private static string BuildPredicates(IReadOnlyList<ApplicationQueryPlanFilter> filters, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters, IApplicationDataSourceProvider provider, List<SugarParameter> sqlParameters, string clause, bool write, ApplicationQueryPlanResolvedModel model)
    {
        var result = new List<string>();
        foreach (var filter in filters)
        {
            var op = filter.Operator.Trim().ToLowerInvariant();
            if (!Operators.Contains(op) || (write && op is not ("eq" or "ne" or "gt" or "gte" or "lt" or "lte"))) throw InvalidPlan("Unsupported filter operator.");
            var field = ResolveField(filter.NodeId, filter.FieldResourceId, model, provider, !write);
            if (op is "isnull" or "isnotnull") { result.Add($"{field} IS {(op == "isnull" ? "NULL" : "NOT NULL")}"); continue; }
            if (!parameters.TryGetValue(filter.ParameterResourceId, out var parameter)) throw InvalidPlan("Filter must bind to a parameter Resource ID.");
            var sqlParameter = AddParameter(parameter, provider, sqlParameters, op);
            result.Add($"{field} {ResolveOperator(op)} {sqlParameter.ParameterName}");
        }
        return result.Count == 0 ? string.Empty : $" {clause} {string.Join(" AND ", result)}";
    }

    private static string ResolveField(string? nodeId, string fieldResourceId, ApplicationQueryPlanResolvedModel model, IApplicationDataSourceProvider provider, bool includeAlias = true)
    {
        if (fieldResourceId == "*") return "*";
        if (string.IsNullOrWhiteSpace(nodeId))
            throw InvalidPlan("Field references must include both node and field Resource IDs.");
        var node = model.ById.TryGetValue(nodeId, out var resolved) ? resolved : throw InvalidPlan("Field references an unknown node Resource ID.");
        var field = node.Resource.Fields.FirstOrDefault(item => string.Equals(item.ResourceId, fieldResourceId, StringComparison.Ordinal));
        if (field is null) throw InvalidPlan($"Field Resource ID '{fieldResourceId}' is not a member of the node Resource ID.");
        var identifier = provider.QuoteIdentifier(field.Name);
        return !includeAlias || string.IsNullOrWhiteSpace(node.Alias) ? identifier : $"{provider.QuoteIdentifier(node.Alias)}.{identifier}";
    }

    private static IReadOnlyDictionary<string, ApplicationQueryPlanParameter> BuildParameters(IReadOnlyList<ApplicationQueryPlanParameter> parameters)
    {
        var result = new Dictionary<string, ApplicationQueryPlanParameter>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.ResourceId) || parameter.ResourceId.Any(char.IsWhiteSpace) || !result.TryAdd(parameter.ResourceId, parameter)) throw InvalidPlan("Parameter Resource IDs must be unique and stable.");
            ApplicationDataSourceSqlNamePolicy.RequireIdentifier(parameter.Name.Trim(), "parameter name");
            if (!ParameterTypes.Contains(parameter.Type)) throw InvalidPlan($"Unsupported parameter type: {parameter.Type}.");
        }
        return result;
    }

    private static SugarParameter ResolveParameter(string resourceId, ApplicationQueryPlanResolvedParameter definition, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters, IApplicationDataSourceProvider provider, List<SugarParameter> sqlParameters)
    {
        if (!parameters.TryGetValue(resourceId, out var supplied))
        {
            if (definition.Required && definition.DefaultValue is null) throw InvalidPlan($"Required mapping cache parameter Resource ID is missing: {resourceId}.");
            supplied = new(resourceId, definition.Name, definition.Type, definition.DefaultValue);
        }
        if (!string.Equals(supplied.Type, definition.Type, StringComparison.OrdinalIgnoreCase)) throw InvalidPlan("Parameter type does not match the mapping cache Resource ID.");
        return AddParameter(supplied, provider, sqlParameters, string.Empty);
    }

    private static SugarParameter AddParameter(ApplicationQueryPlanParameter parameter, IApplicationDataSourceProvider provider, List<SugarParameter> sqlParameters, string operation)
    {
        var sqlName = provider.BuildParameterName(parameter.Name.Trim());
        var value = ConvertParameter(parameter);
        if (operation is "contains" or "startswith" or "endswith") value = ApplyLike(value, operation);
        var existing = sqlParameters.FirstOrDefault(item => string.Equals(item.ParameterName, sqlName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (!Equals(existing.Value, value)) throw InvalidPlan("Parameter names must be unique across Resource IDs.");
            return existing;
        }
        var result = new SugarParameter(sqlName, value);
        sqlParameters.Add(result);
        return result;
    }

    private static IReadOnlyDictionary<string, string> BuildWriteAssignments(ApplicationQueryPlanRequest request, ApplicationQueryPlanResolvedNode node, IReadOnlyDictionary<string, ApplicationQueryPlanParameter> parameters, IApplicationDataSourceProvider provider, List<SugarParameter> sqlParameters)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in request.WriteValues)
        {
            var field = node.Resource.Fields.FirstOrDefault(item => string.Equals(item.ResourceId, pair.Key, StringComparison.Ordinal));
            if (field is null || !parameters.TryGetValue(pair.Value, out var parameter)) throw InvalidPlan("Controlled writes must bind field and value Resource IDs.");
            result[provider.QuoteIdentifier(field.Name)] = AddParameter(parameter, provider, sqlParameters, string.Empty).ParameterName;
        }
        return result;
    }

    private static object? ConvertParameter(ApplicationQueryPlanParameter parameter)
    {
        if (parameter.Value is null) return null;
        try
        {
            return parameter.Type.ToLowerInvariant() switch
            {
                "string" => Convert.ToString(parameter.Value),
                "int" => Convert.ToInt32(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "long" => Convert.ToInt64(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "decimal" => Convert.ToDecimal(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "double" => Convert.ToDouble(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "bool" => Convert.ToBoolean(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "datetime" => DateTime.Parse(Convert.ToString(parameter.Value)!, global::System.Globalization.CultureInfo.InvariantCulture),
                "guid" => Guid.Parse(Convert.ToString(parameter.Value)! ),
                "number" => Convert.ToDecimal(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "boolean" => Convert.ToBoolean(parameter.Value, global::System.Globalization.CultureInfo.InvariantCulture),
                "date" => DateTime.Parse(Convert.ToString(parameter.Value)!, global::System.Globalization.CultureInfo.InvariantCulture, global::System.Globalization.DateTimeStyles.RoundtripKind),
                "json" => ConvertJsonParameter(parameter.Value),
                _ => throw InvalidPlan($"Unsupported parameter type: {parameter.Type}.")
            };
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw InvalidPlan($"Parameter value does not match its type: {parameter.Name}.");
        }
    }

    private static object ConvertJsonParameter(object value)
    {
        if (value is global::System.Text.Json.JsonElement element)
            return element.GetRawText();
        if (value is string text)
        {
            try
            {
                using var document = global::System.Text.Json.JsonDocument.Parse(text);
                return document.RootElement.GetRawText();
            }
            catch (global::System.Text.Json.JsonException)
            {
                throw InvalidPlan("JSON parameter value is invalid.");
            }
        }
        return global::System.Text.Json.JsonSerializer.Serialize(value);
    }

    private static object? ApplyLike(object? value, string operation)
    {
        var text = Convert.ToString(value) ?? string.Empty;
        return operation switch { "contains" => $"%{text}%", "startswith" => $"{text}%", "endswith" => $"%{text}", _ => text };
    }

    private static string RequireIdentifier(string value, string name) => ApplicationDataSourceSqlNamePolicy.RequireIdentifier(value.Trim(), name);
    private static ApplicationQueryJoinType ResolveJoinType(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "inner" => ApplicationQueryJoinType.Inner,
        "left" => ApplicationQueryJoinType.Left,
        "right" => ApplicationQueryJoinType.Right,
        "full" => ApplicationQueryJoinType.Full,
        _ => throw InvalidPlan("Unsupported join type.")
    };
    private static string ResolveOperator(string value) => value switch { "eq" => "=", "ne" => "<>", "gt" => ">", "gte" => ">=", "lt" => "<", "lte" => "<=", "contains" or "startswith" or "endswith" => "LIKE", _ => throw InvalidPlan("Unsupported filter operator.") };
    private static string ResolveDirection(string value) => value.ToLowerInvariant() switch { "asc" => "ASC", "desc" => "DESC", _ => throw InvalidPlan("Sort direction must be asc or desc.") };
    private static ValidationException InvalidPlan(string message) => new(message, ErrorCodes.ApplicationDataCenterInvalidConfig);

    public sealed record CompiledPlan(string PageSql, string CountSql, IReadOnlyList<SugarParameter> Parameters, string Provider, int PageIndex, int PageSize, string? WriteSql, string StatementKind);
}
