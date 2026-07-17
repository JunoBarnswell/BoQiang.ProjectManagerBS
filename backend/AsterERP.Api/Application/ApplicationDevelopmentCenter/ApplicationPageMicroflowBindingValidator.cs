using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Expressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDevelopmentCenter;

public sealed class ApplicationPageMicroflowBindingValidator(
    ApplicationMicroflowOutputSchemaSynchronizer outputSchemaSynchronizer)
{
    private static readonly HashSet<string> AllowedTriggers = new(StringComparer.OrdinalIgnoreCase)
    {
        "pageLoad",
        "manual",
        "formChange"
    };

    private static readonly HashSet<string> AllowedActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "query",
        "detail",
        "add",
        "edit",
        "delete"
    };

    private static readonly HashSet<string> AllowedErrorPolicies = new(StringComparer.OrdinalIgnoreCase)
    {
        "blockDependents",
        "continue",
        "clearOutput"
    };

    private static readonly HashSet<string> AllowedExpressionSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "component",
        "constant",
        "currentRow",
        "form",
        "microflow",
        "page",
        "system",
        "tableRow",
        "variables",
        "workflow"
    };

    public async Task ValidateAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        string documentJson,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(documentJson);
        if (!document.RootElement.TryGetProperty("pageMicroflows", out var bindingsElement))
        {
            return;
        }

        if (bindingsElement.ValueKind != JsonValueKind.Array)
        {
            throw new ValidationException("页面级微流配置必须是数组", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var bindings = bindingsElement.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .ToArray();
        if (bindings.Length == 0)
        {
            return;
        }

        var errors = new List<string>();
        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var flowCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            var alias = ReadString(binding, "alias");
            var flowCode = ReadString(binding, "flowCode");
            if (string.IsNullOrWhiteSpace(alias))
            {
                errors.Add("页面级微流别名不能为空");
            }
            else if (!aliases.Add(alias))
            {
                errors.Add($"页面级微流别名重复: {alias}");
            }

            if (string.IsNullOrWhiteSpace(flowCode))
            {
                errors.Add($"页面级微流 {alias} 缺少 flowCode");
            }
            else
            {
                flowCodes.Add(flowCode);
            }

            ValidateEnum(binding, "trigger", AllowedTriggers, errors, $"页面级微流 {alias} 触发方式无效");
            ValidateEnum(binding, "action", AllowedActions, errors, $"页面级微流 {alias} 执行动作无效");
            ValidateEnum(binding, "errorPolicy", AllowedErrorPolicies, errors, $"页面级微流 {alias} 失败策略无效");
        }

        var contracts = await LoadContractsAsync(db, workspace, flowCodes, cancellationToken);
        foreach (var binding in bindings)
        {
            ValidateBinding(binding, contracts, errors);
        }

        ValidateDependencyGraph(bindings, errors);
        if (errors.Count > 0)
        {
            throw new ValidationException(string.Join("；", errors), ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private async Task<IReadOnlyDictionary<string, ApplicationMicroflowDefinition>> LoadContractsAsync(
        ISqlSugarClient db,
        ApplicationDataCenterWorkspace workspace,
        IReadOnlyCollection<string> flowCodes,
        CancellationToken cancellationToken)
    {
        if (flowCodes.Count == 0)
        {
            return new Dictionary<string, ApplicationMicroflowDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var entities = await db.Queryable<ApplicationMicroflowEntity>()
            .Where(item =>
                item.TenantId == workspace.TenantId &&
                item.AppCode == workspace.AppCode &&
                item.ModuleKey == ApplicationDataCenterModuleKey.Microflow &&
                item.Status == ApplicationDataCenterObjectStatus.Published &&
                !item.IsDeleted &&
                flowCodes.Contains(item.ObjectCode))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, ApplicationMicroflowDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            result[entity.ObjectCode] = outputSchemaSynchronizer.Synchronize(ApplicationMicroflowDefinitionReader.Read(entity.ConfigJson));
        }

        return result;
    }

    private static void ValidateBinding(
        JsonElement binding,
        IReadOnlyDictionary<string, ApplicationMicroflowDefinition> contracts,
        List<string> errors)
    {
        var alias = ReadString(binding, "alias");
        var flowCode = ReadString(binding, "flowCode");
        if (string.IsNullOrWhiteSpace(flowCode) || !contracts.TryGetValue(flowCode, out var definition))
        {
            errors.Add($"页面级微流 {alias} 引用的微流不存在或未发布: {flowCode}");
            return;
        }

        ValidateInputMappings(alias, binding, definition, errors);
        ValidateOutputMappings(alias, binding, definition, errors);
        ValidateRefreshPaths(alias, binding, errors);
    }

    private static void ValidateInputMappings(
        string alias,
        JsonElement binding,
        ApplicationMicroflowDefinition definition,
        List<string> errors)
    {
        var inputCodes = definition.Inputs
            .Select(item => item.VariableCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var action = ReadString(binding, "action");
        foreach (var mapping in EnumerateObjects(binding, "inputMappings"))
        {
            var targetVariable = ReadString(mapping, "targetVariable");
            if (string.IsNullOrWhiteSpace(targetVariable))
            {
                errors.Add($"页面级微流 {alias} 入参映射缺少目标变量");
            }
            else if (!inputCodes.Contains(targetVariable))
            {
                errors.Add($"页面级微流 {alias} 入参不存在: {targetVariable}");
            }

            if (mapping.TryGetProperty("sourceExpression", out var expression))
            {
                ValidateDesignerExpression(expression, $"页面级微流 {alias} 入参 {targetVariable}", errors);
                if (UsesCurrentRow(expression) && !new[] { "detail", "edit", "delete" }.Contains(action, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"页面级微流 {alias} 在 {action} 动作中引用 currentRow，但该动作没有行上下文");
                }
            }
            else if (mapping.TryGetProperty("required", out var required) &&
                     required.ValueKind == JsonValueKind.True)
            {
                errors.Add($"页面级微流 {alias} 必填入参 {targetVariable} 缺少来源表达式");
            }
        }
    }

    private static void ValidateOutputMappings(
        string alias,
        JsonElement binding,
        ApplicationMicroflowDefinition definition,
        List<string> errors)
    {
        var outputs = definition.Outputs.ToDictionary(item => item.VariableCode, StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in EnumerateObjects(binding, "outputMappings"))
        {
            var outputVariable = ReadString(mapping, "outputVariable");
            if (string.IsNullOrWhiteSpace(outputVariable))
            {
                errors.Add($"页面级微流 {alias} 输出映射缺少输出变量");
                continue;
            }

            if (!outputs.TryGetValue(outputVariable, out var output))
            {
                errors.Add($"页面级微流 {alias} 输出不存在: {outputVariable}");
                continue;
            }

            var resultPath = ReadOptionalString(mapping, "resultPath");
            if (!string.IsNullOrWhiteSpace(resultPath) && !CanResolveResultPath(output, resultPath))
            {
                errors.Add($"页面级微流 {alias} 输出路径不存在: {outputVariable}.{resultPath}");
            }

            var writeTo = ReadString(mapping, "writeTo");
            if (string.IsNullOrWhiteSpace(writeTo))
            {
                errors.Add($"页面级微流 {alias} 输出 {outputVariable} 缺少写入路径");
            }
            else if (!writeTo.StartsWith($"microflows.{alias}.", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"页面级微流 {alias} 输出写入路径必须位于 microflows.{alias}");
            }
        }
    }

    private static void ValidateRefreshPaths(string alias, JsonElement binding, List<string> errors)
    {
        if (!binding.TryGetProperty("refreshOnChangePaths", out var paths))
        {
            return;
        }

        if (paths.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"页面级微流 {alias} 刷新依赖必须是数组");
            return;
        }

        foreach (var path in paths.EnumerateArray())
        {
            if (path.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(path.GetString()))
            {
                errors.Add($"页面级微流 {alias} 刷新依赖包含空路径");
            }
        }
    }

    private static void ValidateDependencyGraph(IReadOnlyCollection<JsonElement> bindings, List<string> errors)
    {
        var aliases = bindings
            .Select(binding => ReadString(binding, "alias"))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var graph = aliases.ToDictionary(alias => alias, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            var alias = ReadString(binding, "alias");
            if (string.IsNullOrWhiteSpace(alias) || !graph.ContainsKey(alias))
            {
                continue;
            }

            foreach (var mapping in EnumerateObjects(binding, "inputMappings"))
            {
                if (!mapping.TryGetProperty("sourceExpression", out var expression))
                {
                    continue;
                }

                var dependencyAlias = ReadMicroflowAliasReference(expression);
                if (!string.IsNullOrWhiteSpace(dependencyAlias) && aliases.Contains(dependencyAlias))
                {
                    graph[dependencyAlias].Add(alias);
                }
            }
        }

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            if (HasCycle(alias, graph, visiting, visited))
            {
                errors.Add("页面级微流依赖存在循环");
                return;
            }
        }
    }

    private static bool HasCycle(
        string alias,
        IReadOnlyDictionary<string, HashSet<string>> graph,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(alias))
        {
            return false;
        }

        if (!visiting.Add(alias))
        {
            return true;
        }

        if (graph.TryGetValue(alias, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                if (HasCycle(dependent, graph, visiting, visited))
                {
                    return true;
                }
            }
        }

        visiting.Remove(alias);
        visited.Add(alias);
        return false;
    }

    private static string? ReadMicroflowAliasReference(JsonElement expression)
    {
        if (expression.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (IsCanonicalExpression(expression))
        {
            return ReadCanonicalMicroflowAliasReference(expression);
        }

        var source = ReadString(expression, "source");
        var path = ReadOptionalString(expression, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (string.Equals(source, "microflow", StringComparison.OrdinalIgnoreCase))
        {
            return path.Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }

        if (string.Equals(source, "variables", StringComparison.OrdinalIgnoreCase) &&
            path.StartsWith("microflows.", StringComparison.OrdinalIgnoreCase))
        {
            return path["microflows.".Length..].Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }

        return null;
    }

    private static bool CanResolveResultPath(ApplicationMicroflowVariableDefinition output, string resultPath)
    {
        if (string.Equals(resultPath, output.VariableCode, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resultPath, "data", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return output.Fields.Any(field => string.Equals(field.FieldCode, resultPath, StringComparison.OrdinalIgnoreCase)) ||
               output.Fields.Any(field => resultPath.EndsWith($".{field.FieldCode}", StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateDesignerExpression(JsonElement expression, string label, List<string> errors)
    {
        if (expression.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{label} 表达式格式无效");
            return;
        }

        if (IsCanonicalExpression(expression))
        {
            ValidateCanonicalExpression(expression, label, errors);
            return;
        }

        var source = ReadString(expression, "source");
        if (string.IsNullOrWhiteSpace(source) || !AllowedExpressionSources.Contains(source))
        {
            errors.Add($"{label} 表达式来源无效: {source}");
        }

        if (expression.TryGetProperty("rawPathPreview", out var rawPathPreview) &&
            rawPathPreview.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(rawPathPreview.GetString()))
        {
            errors.Add($"{label} 表达式包含未确认的原始路径，请重新从变量树选择");
        }
    }

    private static bool IsCanonicalExpression(JsonElement expression) =>
        string.Equals(ReadString(expression, "version"), "latest", StringComparison.Ordinal) &&
        !string.IsNullOrWhiteSpace(ReadString(expression, "kind"));

    private static void ValidateCanonicalExpression(JsonElement expression, string label, List<string> errors)
    {
        try
        {
            var value = JsonSerializer.Deserialize<ExpressionValueDto>(expression.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (value is null)
            {
                errors.Add($"{label} 表达式格式无效");
                return;
            }

            ExpressionValueContractValidator.Validate(value);
            foreach (var resourceId in CollectCanonicalResourceIds(expression))
            {
                var source = resourceId.Split(':', 2, StringSplitOptions.TrimEntries)[0];
                if (!AllowedExpressionSources.Contains(source))
                {
                    errors.Add($"{label} 表达式资源来源无效: {source}");
                }
            }
        }
        catch (ValidationException exception)
        {
            errors.Add($"{label} 表达式无效: {exception.Message}");
        }
        catch (JsonException)
        {
            errors.Add($"{label} 表达式格式无效");
        }
    }

    private static string? ReadCanonicalMicroflowAliasReference(JsonElement expression)
    {
        if (!string.Equals(ReadString(expression, "kind"), "resourceRef", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var child in EnumerateCanonicalChildren(expression))
            {
                var alias = ReadCanonicalMicroflowAliasReference(child);
                if (!string.IsNullOrWhiteSpace(alias)) return alias;
            }

            return null;
        }

        var resourceId = ReadOptionalString(expression, "resourceId");
        if (string.IsNullOrWhiteSpace(resourceId)) return null;
        var parts = resourceId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2) return null;
        if (string.Equals(parts[0], "microflow", StringComparison.OrdinalIgnoreCase)) return parts[1];
        if (string.Equals(parts[0], "variables", StringComparison.OrdinalIgnoreCase) &&
            parts.Length >= 3 && string.Equals(parts[1], "microflows", StringComparison.OrdinalIgnoreCase))
        {
            return parts[2];
        }

        return null;
    }

    private static IEnumerable<string> CollectCanonicalResourceIds(JsonElement expression)
    {
        if (expression.ValueKind != JsonValueKind.Object) yield break;
        if (string.Equals(ReadString(expression, "kind"), "resourceRef", StringComparison.OrdinalIgnoreCase))
        {
            var resourceId = ReadOptionalString(expression, "resourceId");
            if (!string.IsNullOrWhiteSpace(resourceId)) yield return resourceId;
        }

        foreach (var child in EnumerateCanonicalChildren(expression))
        {
            foreach (var resourceId in CollectCanonicalResourceIds(child)) yield return resourceId;
        }
    }

    private static IEnumerable<JsonElement> EnumerateCanonicalChildren(JsonElement expression)
    {
        foreach (var property in expression.EnumerateObject())
        {
            if (property.NameEquals("resourceId") || property.NameEquals("dependencies") || property.NameEquals("canonicalHash")) continue;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                yield return property.Value;
            }
            else if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object) yield return item;
                }
            }
        }
    }

    private static bool UsesCurrentRow(JsonElement expression)
    {
        if (IsCanonicalExpression(expression))
            return CollectCanonicalResourceIds(expression).Any(id => id.StartsWith("currentRow:", StringComparison.OrdinalIgnoreCase) || id.StartsWith("tableRow:", StringComparison.OrdinalIgnoreCase));
        return string.Equals(ReadString(expression, "source"), "currentRow", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(ReadString(expression, "source"), "tableRow", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).ToArray();
    }

    private static void ValidateEnum(
        JsonElement value,
        string propertyName,
        HashSet<string> allowedValues,
        List<string> errors,
        string message)
    {
        var rawValue = ReadString(value, propertyName);
        if (!string.IsNullOrWhiteSpace(rawValue) && !allowedValues.Contains(rawValue))
        {
            errors.Add($"{message}: {rawValue}");
        }
    }

    private static string ReadString(JsonElement value, string propertyName) =>
        ReadOptionalString(value, propertyName) ?? string.Empty;

    private static string? ReadOptionalString(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = property.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
