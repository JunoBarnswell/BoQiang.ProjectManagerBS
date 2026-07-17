using System.Text.Json;
using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowExpressionReferenceValidator(RuntimeExpressionHelperCatalog functionCatalog)
{
    public void Validate(ApplicationMicroflowDefinition definition, List<string> errors)
    {
        var variables = BuildVariableCatalog(definition);
        var localSources = BuildLocalSourceCatalog(definition);
        foreach (var node in definition.Nodes)
        {
            if (ApplicationMicroflowGlobalVariableNodeReader.IsGlobalVariableNode(node))
            {
                continue;
            }

            ValidateSetVariableTarget(node, variables, errors);
            foreach (var locatedExpression in CollectExpressions(node.Config, $"node[{node.Id}].config", errors))
            {
                ValidateExpression(node, locatedExpression.Expression, locatedExpression.Path, variables, localSources, errors);
            }
        }
    }

    private static HashSet<string> BuildLocalSourceCatalog(ApplicationMicroflowDefinition definition)
    {
        var localSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "currentRow",
            "row",
            "item",
            "model",
            "system",
            "runtime",
            "context",
            "currentUser"
        };

        foreach (var node in definition.Nodes.Where(item => string.Equals(item.Type, "loop", StringComparison.OrdinalIgnoreCase)))
        {
            var itemVariable = ReadConfigString(node.Config, "itemVariable");
            if (!string.IsNullOrWhiteSpace(itemVariable))
            {
                localSources.Add(itemVariable);
            }
        }

        return localSources;
    }

    private static Dictionary<string, VariableDescriptor> BuildVariableCatalog(ApplicationMicroflowDefinition definition)
    {
        var variables = new Dictionary<string, VariableDescriptor>(StringComparer.OrdinalIgnoreCase);
        AddVariables(definition.Inputs, false);
        AddVariables(definition.Variables, true);
        AddVariables(ApplicationMicroflowGlobalVariableNodeReader.ReadVariables(definition), true);
        AddVariables(definition.Outputs, true);

        foreach (var node in definition.Nodes)
        {
            var variableCode = ReadNodeOutputVariableCode(node);
            if (string.IsNullOrWhiteSpace(variableCode))
            {
                continue;
            }

            var schema = ReadJson<ApplicationMicroflowOutputSchemaDefinition>(node.Config, "outputSchema");
            var nodeFields = schema?.Fields ?? ResolveDomainObjectFields(definition, node);
            variables[variableCode] = variables.TryGetValue(variableCode, out var existing)
                ? existing with { Fields = MergeFields(existing.Fields, nodeFields) }
                : new VariableDescriptor(nodeFields, false);
        }

        return variables;

        void AddVariables(IEnumerable<ApplicationMicroflowVariableDefinition> source, bool writable)
        {
            foreach (var variable in source)
            {
                var variableCode = variable.VariableCode?.Trim();
                if (string.IsNullOrWhiteSpace(variableCode))
                {
                    continue;
                }

                variables[variableCode] = new VariableDescriptor(variable.Fields ?? [], writable);
            }
        }
    }

    private static void ValidateSetVariableTarget(
        ApplicationMicroflowNodeDefinition node,
        IReadOnlyDictionary<string, VariableDescriptor> variables,
        List<string> errors)
    {
        if (!string.Equals(node.Type, "setVariable", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var targetPath = ReadConfigString(node.Config, "variableCode");
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            errors.Add($"Set Variable 节点 {node.Name}({node.Id}) 未选择目标变量");
            return;
        }

        var parts = targetPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !variables.TryGetValue(parts[0], out var descriptor))
        {
            errors.Add($"Set Variable 节点 {node.Name}({node.Id}) 目标变量不存在: {targetPath}");
            return;
        }

        if (!descriptor.Writable)
        {
            errors.Add($"Set Variable 节点 {node.Name}({node.Id}) 目标变量不可写: {targetPath}");
            return;
        }

        if (parts.Length <= 1 || descriptor.Fields.Count == 0)
        {
            return;
        }

        var fieldPath = string.Join('.', parts.Skip(1));
        var field = FindField(descriptor.Fields, fieldPath);
        if (field is null && !HasNestedFieldPrefix(descriptor.Fields, fieldPath))
        {
            errors.Add($"Set Variable 节点 {node.Name}({node.Id}) 目标字段不存在: {targetPath}");
            return;
        }

        if (field is not null && (!field.Writable || field.ReadOnly))
        {
            errors.Add($"Set Variable 节点 {node.Name}({node.Id}) 目标字段不可写: {targetPath}");
        }
    }

    private void ValidateExpression(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto expression,
        string path,
        IReadOnlyDictionary<string, VariableDescriptor> variables,
        HashSet<string> localSources,
        List<string> errors)
    {
        var kind = expression.Kind?.Trim();
        if (string.IsNullOrWhiteSpace(kind))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 缺少 kind");
            return;
        }

        switch (kind.ToLowerInvariant())
        {
            case "literal":
                return;
            case "ref":
                ValidateReference(node, expression, path, variables, localSources, errors);
                return;
            case "function":
                ValidateFunction(node, expression, path, variables, localSources, errors);
                return;
            case "object":
                foreach (var property in expression.Properties)
                {
                    ValidateExpression(node, property.Value, $"{path}.properties.{property.Key}", variables, localSources, errors);
                }
                return;
            case "array":
            case "template":
                for (var index = 0; index < expression.Items.Count; index++)
                {
                    ValidateExpression(node, expression.Items[index], $"{path}.items[{index}]", variables, localSources, errors);
                }
                return;
            default:
                errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 使用了不支持的 kind: {kind}");
                return;
        }
    }

    private void ValidateFunction(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto expression,
        string path,
        IReadOnlyDictionary<string, VariableDescriptor> variables,
        HashSet<string> localSources,
        List<string> errors)
    {
        var functionId = expression.FunctionId?.Trim();
        if (string.IsNullOrWhiteSpace(functionId))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 缺少 functionId");
        }
        else if (!functionCatalog.Supports(functionId))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 使用了不支持的函数: {functionId}");
        }

        for (var index = 0; index < expression.Args.Count; index++)
        {
            ValidateExpression(node, expression.Args[index], $"{path}.args[{index}]", variables, localSources, errors);
        }
    }

    private static void ValidateReference(
        ApplicationMicroflowNodeDefinition node,
        RuntimeValueExpressionDto expression,
        string path,
        IReadOnlyDictionary<string, VariableDescriptor> variables,
        HashSet<string> localSources,
        List<string> errors)
    {
        var reference = expression.Ref;
        if (reference is null)
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 缺少 ref");
            return;
        }

        var sourceType = reference.SourceType?.Trim();
        if (string.IsNullOrWhiteSpace(sourceType))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 缺少 ref.sourceType");
            return;
        }

        if (IsLocalSource(sourceType, reference, localSources))
        {
            return;
        }

        if (IsSqlResultSource(sourceType))
        {
            return;
        }

        if (!IsVariableSource(sourceType))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 来源不支持: {sourceType}");
            return;
        }

        var variableCode = ReadReferenceVariableCode(reference);
        if (string.IsNullOrWhiteSpace(variableCode) || !variables.TryGetValue(variableCode, out var descriptor))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 引用变量不存在: {FormatReference(reference)}");
            return;
        }

        if (reference.FieldPath.Count == 0 || descriptor.Fields.Count == 0)
        {
            return;
        }

        var fieldPath = string.Join('.', reference.FieldPath);
        if (FindField(descriptor.Fields, fieldPath) is null && !HasNestedFieldPrefix(descriptor.Fields, fieldPath))
        {
            errors.Add($"{node.Type} 节点 {node.Name}({node.Id}) 表达式 {path} 引用字段不存在: {FormatReference(reference)}");
        }
    }

    private static bool IsLocalSource(string sourceType, RuntimeVariableRefDto reference, HashSet<string> localSources)
    {
        if (localSources.Contains(sourceType))
        {
            return true;
        }

        var variableId = reference.VariableId?.Trim();
        var outputKey = reference.OutputKey?.Trim();
        return !string.IsNullOrWhiteSpace(variableId) && localSources.Contains(variableId) ||
            !string.IsNullOrWhiteSpace(outputKey) && localSources.Contains(outputKey);
    }

    private static bool IsVariableSource(string sourceType)
    {
        var normalized = sourceType.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized is "trigger" or "nodeoutput" or "nodeinput" or "global";
    }

    private static bool IsSqlResultSource(string sourceType)
    {
        var normalized = sourceType.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();
        return normalized is "sqlresult";
    }

    private static string ReadReferenceVariableCode(RuntimeVariableRefDto reference)
    {
        if (!string.IsNullOrWhiteSpace(reference.OutputKey))
        {
            return reference.OutputKey.Trim();
        }

        return reference.VariableId?.Trim() ?? string.Empty;
    }

    private static string FormatReference(RuntimeVariableRefDto reference)
    {
        var root = ReadReferenceVariableCode(reference);
        return reference.FieldPath.Count == 0 ? root : $"{root}.{string.Join('.', reference.FieldPath)}";
    }

    private static IEnumerable<LocatedExpression> CollectExpressions(object? value, string path, List<string> errors)
    {
        if (TryReadExpression(value, out var expression) && expression is not null)
        {
            yield return new LocatedExpression(expression, path);
            yield break;
        }

        if (value is ApplicationMicroflowOutputSchemaDefinition outputSchema)
        {
            if (outputSchema.ArrayExpression is not null)
            {
                yield return new LocatedExpression(outputSchema.ArrayExpression, $"{path}.arrayExpression");
            }

            for (var index = 0; index < outputSchema.Fields.Count; index++)
            {
                foreach (var item in CollectExpressions(outputSchema.Fields[index], $"{path}.fields[{index}]", errors))
                {
                    yield return item;
                }
            }
            yield break;
        }

        if (value is ApplicationMicroflowFieldDefinition field)
        {
            if (field.Expression is not null)
            {
                yield return new LocatedExpression(field.Expression, $"{path}.expression");
            }
            yield break;
        }

        if (value is ApplicationMicroflowDataMappingDefinition mapping)
        {
            if (mapping.Expression is not null)
            {
                yield return new LocatedExpression(mapping.Expression, $"{path}.expression");
            }
            yield break;
        }

        if (value is ApplicationMicroflowCompositeChildCreateDefinition childCreate)
        {
            if (childCreate.RowsExpression is not null)
            {
                yield return new LocatedExpression(childCreate.RowsExpression, $"{path}.rowsExpression");
            }

            for (var index = 0; index < childCreate.FieldMappings.Count; index++)
            {
                foreach (var item in CollectExpressions(childCreate.FieldMappings[index], $"{path}.fieldMappings[{index}]", errors))
                {
                    yield return item;
                }
            }
            yield break;
        }

        if (value is ApplicationMicroflowCompositeChildUpdateDefinition childUpdate)
        {
            if (childUpdate.RowsExpression is not null)
            {
                yield return new LocatedExpression(childUpdate.RowsExpression, $"{path}.rowsExpression");
            }

            if (childUpdate.DeleteIdsExpression is not null)
            {
                yield return new LocatedExpression(childUpdate.DeleteIdsExpression, $"{path}.deleteIdsExpression");
            }

            for (var index = 0; index < childUpdate.FieldMappings.Count; index++)
            {
                foreach (var item in CollectExpressions(childUpdate.FieldMappings[index], $"{path}.fieldMappings[{index}]", errors))
                {
                    yield return item;
                }
            }
            yield break;
        }

        if (value is ApplicationMicroflowCompositeChildDeleteDefinition childDelete)
        {
            if (childDelete.ParentIdExpression is not null)
            {
                yield return new LocatedExpression(childDelete.ParentIdExpression, $"{path}.parentIdExpression");
            }
            yield break;
        }

        if (value is RuntimeModelFieldMappingDto runtimeMapping)
        {
            if (runtimeMapping.Expression is not null)
            {
                yield return new LocatedExpression(runtimeMapping.Expression, $"{path}.expression");
            }
            yield break;
        }

        if (value is RuntimeModelFilterMappingDto filterMapping)
        {
            if (filterMapping.ValueExpression is not null)
            {
                yield return new LocatedExpression(filterMapping.ValueExpression, $"{path}.valueExpression");
            }

            if (filterMapping.ValueToExpression is not null)
            {
                yield return new LocatedExpression(filterMapping.ValueToExpression, $"{path}.valueToExpression");
            }
            yield break;
        }

        if (value is JsonElement element)
        {
            foreach (var item in CollectExpressions(element, path, errors))
            {
                yield return item;
            }
            yield break;
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            if (LooksLikeLegacyExpression(dictionary))
            {
                errors.Add($"表达式 {path} 使用旧协议 source/path/helpers，必须改为 kind/ref/properties/items/args/value");
                yield break;
            }

            foreach (var pair in dictionary)
            {
                foreach (var item in CollectExpressions(pair.Value, $"{path}.{pair.Key}", errors))
                {
                    yield return item;
                }
            }
            yield break;
        }

        if (value is IEnumerable<object?> array)
        {
            var index = 0;
            foreach (var itemValue in array)
            {
                foreach (var item in CollectExpressions(itemValue, $"{path}[{index}]", errors))
                {
                    yield return item;
                }
                index += 1;
            }
        }
    }

    private static IEnumerable<LocatedExpression> CollectExpressions(JsonElement element, string path, List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("kind", out _))
        {
            var expression = JsonSerializer.Deserialize<RuntimeValueExpressionDto>(element.GetRawText(), ApplicationDataCenterJson.Options);
            if (expression is not null)
            {
                yield return new LocatedExpression(expression, path);
            }
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object && LooksLikeLegacyExpression(element))
        {
            errors.Add($"表达式 {path} 使用旧协议 source/path/helpers，必须改为 kind/ref/properties/items/args/value");
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                foreach (var item in CollectExpressions(property.Value, $"{path}.{property.Name}", errors))
                {
                    yield return item;
                }
            }
            yield break;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var itemElement in element.EnumerateArray())
            {
                foreach (var item in CollectExpressions(itemElement, $"{path}[{index}]", errors))
                {
                    yield return item;
                }
                index += 1;
            }
        }
    }

    private static bool TryReadExpression(object? value, out RuntimeValueExpressionDto? expression)
    {
        expression = null;
        if (value is RuntimeValueExpressionDto runtimeExpression)
        {
            expression = runtimeExpression;
            return true;
        }

        return false;
    }

    private static bool LooksLikeLegacyExpression(IDictionary<string, object?> dictionary) =>
        !dictionary.ContainsKey("kind") &&
        (dictionary.ContainsKey("source") || dictionary.ContainsKey("path") || dictionary.ContainsKey("helpers"));

    private static bool LooksLikeLegacyExpression(JsonElement element) =>
        !element.TryGetProperty("kind", out _) &&
        (element.TryGetProperty("source", out _) || element.TryGetProperty("path", out _) || element.TryGetProperty("helpers", out _));

    private static T? ReadJson<T>(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return default;
        }

        return value is JsonElement element
            ? JsonSerializer.Deserialize<T>(element.GetRawText(), ApplicationDataCenterJson.Options)
            : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, ApplicationDataCenterJson.Options), ApplicationDataCenterJson.Options);
    }

    private static string ReadConfigString(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || value is null)
        {
            return string.Empty;
        }

        return value is JsonElement element && element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : value.ToString() ?? string.Empty;
    }

    private static string ReadNodeOutputVariableCode(ApplicationMicroflowNodeDefinition node)
    {
        if (string.Equals(node.Type, "return", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.Type, "end", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(node.Type, "decision", StringComparison.OrdinalIgnoreCase) ||
            ApplicationMicroflowGlobalVariableNodeReader.IsGlobalVariableNode(node))
        {
            return string.Empty;
        }

        if (string.Equals(node.Type, "loop", StringComparison.OrdinalIgnoreCase))
        {
            return ReadConfigString(node.Config, "itemVariable");
        }

        if (string.Equals(node.Type, "setVariable", StringComparison.OrdinalIgnoreCase))
        {
            return ReadRootVariableCode(ReadConfigString(node.Config, "variableCode"));
        }

        var targetVariable = ReadConfigString(node.Config, "targetVariable");
        return string.IsNullOrWhiteSpace(targetVariable)
            ? ReadConfigString(node.Config, "variableCode")
            : targetVariable;
    }

    private static string ReadRootVariableCode(string variablePath)
    {
        return variablePath
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
    }

    private static ApplicationMicroflowFieldDefinition? FindField(
        IReadOnlyList<ApplicationMicroflowFieldDefinition> fields,
        string fieldPath)
    {
        return fields.FirstOrDefault(item =>
            string.Equals(item.FieldCode?.Trim(), fieldPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasNestedFieldPrefix(
        IReadOnlyList<ApplicationMicroflowFieldDefinition> fields,
        string fieldPath)
    {
        var prefix = $"{fieldPath}.";
        return fields.Any(item =>
            item.FieldCode?.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static IReadOnlyList<ApplicationMicroflowFieldDefinition> MergeFields(
        IReadOnlyList<ApplicationMicroflowFieldDefinition> left,
        IReadOnlyList<ApplicationMicroflowFieldDefinition> right)
    {
        if (left.Count == 0)
        {
            return right;
        }

        if (right.Count == 0)
        {
            return left;
        }

        var fields = new List<ApplicationMicroflowFieldDefinition>(left.Count + right.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in left.Concat(right))
        {
            var fieldCode = field.FieldCode?.Trim();
            if (string.IsNullOrWhiteSpace(fieldCode) || !seen.Add(fieldCode))
            {
                continue;
            }

            fields.Add(field);
        }

        return fields;
    }

    private static IReadOnlyList<ApplicationMicroflowFieldDefinition> ResolveDomainObjectFields(
        ApplicationMicroflowDefinition definition,
        ApplicationMicroflowNodeDefinition node)
    {
        var modelCode = ReadConfigString(node.Config, "modelCode");
        if (string.IsNullOrWhiteSpace(modelCode))
        {
            modelCode = ReadConfigString(node.Config, "rootModelCode");
        }

        if (string.IsNullOrWhiteSpace(modelCode))
        {
            return [];
        }

        return definition.DomainObjects
            .FirstOrDefault(item =>
                string.Equals(item.ObjectCode, modelCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(item.ModelCode, modelCode, StringComparison.OrdinalIgnoreCase))
            ?.Fields ?? [];
    }

    private readonly record struct VariableDescriptor(IReadOnlyList<ApplicationMicroflowFieldDefinition> Fields, bool Writable);

    private readonly record struct LocatedExpression(RuntimeValueExpressionDto Expression, string Path);
}
