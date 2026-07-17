using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using System.Text.Json;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowDefinitionValidator(
    ApplicationMicroflowExpressionReferenceValidator? expressionReferenceValidator = null,
    ApplicationDataCenterSqlScriptValidator? sqlScriptValidator = null)
{
    private static readonly HashSet<string> NodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "start",
        "end",
        "decision",
        "loop",
        "query",
        "retrieve",
        "detail",
        "compositeDetail",
        "create",
        "compositeCreate",
        "compositeUpdate",
        "change",
        "delete",
        "compositeDelete",
        "callApi",
        "setVariable",
        ApplicationMicroflowGlobalVariableNodeReader.NodeType,
        "return"
    };

    public IReadOnlyList<string> Validate(ApplicationMicroflowDefinition definition)
    {
        var errors = new List<string>();
        if (definition.SchemaVersion <= 0)
        {
            errors.Add("schemaVersion 必须大于 0");
        }

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in definition.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                errors.Add("节点 ID 不能为空");
                continue;
            }

            if (!nodeIds.Add(node.Id))
            {
                errors.Add($"节点 ID 重复: {node.Id}");
            }

            if (node.Type.Equals("runSql", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("runSql 节点已移除，请改用 Return 节点 SQL Script");
            }
            else if (!NodeTypes.Contains(node.Type))
            {
                errors.Add($"不支持的微流节点类型: {node.Type}");
            }
        }

        if (!definition.Nodes.Any(item => string.Equals(item.Type, "start", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("微流必须包含 Start 节点");
        }

        if (!definition.Nodes.Any(item => string.Equals(item.Type, "end", StringComparison.OrdinalIgnoreCase) || string.Equals(item.Type, "return", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("微流必须包含 End 或 Return 节点");
        }

        ValidateVariables(definition, errors);
        ValidateGlobalVariableNodes(definition, errors);
        ValidateReturnNodes(definition, errors);

        foreach (var edge in definition.Edges)
        {
            if (!nodeIds.Contains(edge.SourceNodeId))
            {
                errors.Add($"连线源节点不存在: {edge.SourceNodeId}");
            }

            if (!nodeIds.Contains(edge.TargetNodeId))
            {
                errors.Add($"连线目标节点不存在: {edge.TargetNodeId}");
            }

            var sourceNode = definition.Nodes.FirstOrDefault(item => string.Equals(item.Id, edge.SourceNodeId, StringComparison.OrdinalIgnoreCase));
            var targetNode = definition.Nodes.FirstOrDefault(item => string.Equals(item.Id, edge.TargetNodeId, StringComparison.OrdinalIgnoreCase));
            if (sourceNode is not null && ApplicationMicroflowGlobalVariableNodeReader.IsGlobalVariableNode(sourceNode))
            {
                errors.Add($"全局变量节点不能作为连线源: {sourceNode.Name}({sourceNode.Id})");
            }

            if (targetNode is not null && ApplicationMicroflowGlobalVariableNodeReader.IsGlobalVariableNode(targetNode))
            {
                errors.Add($"全局变量节点不能作为连线目标: {targetNode.Name}({targetNode.Id})");
            }
        }

        foreach (var endpoint in definition.ApiEndpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.EndpointCode))
            {
                errors.Add("接口端点编码不能为空");
            }

            if (string.IsNullOrWhiteSpace(endpoint.RoutePath))
            {
                errors.Add($"端点 {endpoint.EndpointCode} 缺少接口路径");
            }
        }

        expressionReferenceValidator?.Validate(definition, errors);
        return errors;
    }

    public void EnsureValid(ApplicationMicroflowDefinition definition)
    {
        var errors = Validate(definition);
        if (errors.Count > 0)
        {
            throw new ValidationException(string.Join("；", errors), ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private void ValidateReturnNodes(ApplicationMicroflowDefinition definition, List<string> errors)
    {
        foreach (var node in definition.Nodes.Where(item => string.Equals(item.Type, "return", StringComparison.OrdinalIgnoreCase)))
        {
            var schema = ReadJson<ApplicationMicroflowOutputSchemaDefinition>(node.Config, "outputSchema");
            if (schema is null)
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) 缺少返回结构 outputSchema");
                continue;
            }

            var variableCode = schema.VariableCode.Trim();
            if (string.IsNullOrWhiteSpace(variableCode))
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) 返回变量编码不能为空");
            }

            if (schema.Fields.Count == 0)
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) 未配置返回字段");
                continue;
            }

            var sourceMode = string.IsNullOrWhiteSpace(schema.SourceMode) ? "fields" : schema.SourceMode.Trim();
            if (!string.Equals(sourceMode, "fields", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(sourceMode, "sqlScript", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) 返回来源模式无效: {schema.SourceMode}");
            }

            if (string.Equals(sourceMode, "fields", StringComparison.OrdinalIgnoreCase) &&
                schema.ArrayExpression is not null && !HasValueExpression(schema.ArrayExpression))
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) 数组来源表达式无效");
            }

            if (string.Equals(sourceMode, "sqlScript", StringComparison.OrdinalIgnoreCase))
            {
                if (schema.ArrayExpression is not null)
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) SQL 脚本模式不能配置字段模式数组来源");
                }

                if (sqlScriptValidator is null)
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) SQL 脚本校验器未注册");
                }
                else
                {
                    sqlScriptValidator.Validate(node, schema, errors);
                }
            }
            else if (sqlScriptValidator?.HasConfiguredSqlScript(schema.SqlScript) == true)
            {
                errors.Add($"Return 节点 {node.Name}({node.Id}) 字段配置模式不能同时配置 SQL 脚本");
            }

            var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < schema.Fields.Count; index++)
            {
                var field = schema.Fields[index];
                var fieldCode = field.FieldCode.Trim();
                if (string.IsNullOrWhiteSpace(fieldCode))
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) 第 {index + 1} 个返回字段编码不能为空");
                    continue;
                }

                if (!fieldCodes.Add(fieldCode))
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) 返回字段编码重复: {fieldCode}");
                }

                if (string.IsNullOrWhiteSpace(field.FieldName))
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) 字段 {fieldCode} 显示名称不能为空");
                }

                if (string.IsNullOrWhiteSpace(field.DataType))
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) 字段 {fieldCode} 数据类型不能为空");
                }

                if (!HasValueExpression(field.Expression))
                {
                    errors.Add($"Return 节点 {node.Name}({node.Id}) 字段 {fieldCode} 缺少来源表达式");
                }
            }
        }
    }

    private static void ValidateVariables(ApplicationMicroflowDefinition definition, List<string> errors)
    {
        var domainObjectCodes = definition.DomainObjects
            .Select(item => item.ObjectCode?.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateVariableCollection("输入变量", definition.Inputs, domainObjectCodes, errors);
        ValidateVariableCollection("过程变量", definition.Variables, domainObjectCodes, errors);
        ValidateVariableCollection("输出变量", definition.Outputs, domainObjectCodes, errors);

        var allVariableCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in definition.Inputs.Concat(definition.Variables).Concat(definition.Outputs))
        {
            var variableCode = variable.VariableCode?.Trim();
            if (string.IsNullOrWhiteSpace(variableCode))
            {
                continue;
            }

            if (!allVariableCodes.Add(variableCode))
            {
                errors.Add($"变量编码跨集合重复: {variableCode}");
            }
        }
    }

    private static void ValidateGlobalVariableNodes(ApplicationMicroflowDefinition definition, List<string> errors)
    {
        var globalVariables = ApplicationMicroflowGlobalVariableNodeReader.ReadVariables(definition);
        var globalVariableCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in globalVariables)
        {
            var variableCode = variable.VariableCode?.Trim();
            if (string.IsNullOrWhiteSpace(variableCode))
            {
                errors.Add($"全局变量节点 {variable.SourceNodeId} 存在空变量编码");
                continue;
            }

            if (variableCode.Contains('.', StringComparison.Ordinal))
            {
                errors.Add($"全局变量节点 {variable.SourceNodeId} 的变量编码 {variableCode} 不能包含点路径");
            }

            if (!globalVariableCodes.Add(variableCode))
            {
                errors.Add($"全局变量编码重复: {variableCode}");
            }
        }

        var rootVariableCodes = definition.Inputs
            .Concat(definition.Variables.Where(variable => string.IsNullOrWhiteSpace(variable.SourceNodeId)))
            .Concat(definition.Outputs)
            .Select(variable => variable.VariableCode?.Trim())
            .Where(variableCode => !string.IsNullOrWhiteSpace(variableCode))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var variableCode in globalVariableCodes)
        {
            if (rootVariableCodes.Contains(variableCode))
            {
                errors.Add($"全局变量编码与根变量重复: {variableCode}");
            }
        }
    }

    private static void ValidateVariableCollection(
        string collectionName,
        IReadOnlyList<ApplicationMicroflowVariableDefinition> variables,
        HashSet<string> domainObjectCodes,
        List<string> errors)
    {
        var variableCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            var variableCode = variable.VariableCode?.Trim();
            if (string.IsNullOrWhiteSpace(variableCode))
            {
                errors.Add($"{collectionName}编码不能为空");
                continue;
            }

            if (variableCode.Contains('.', StringComparison.Ordinal))
            {
                errors.Add($"{collectionName} {variableCode} 不能使用点路径作为变量编码，请定义根变量并在字段或表达式中使用路径");
            }

            if (!variableCodes.Add(variableCode))
            {
                errors.Add($"{collectionName}编码重复: {variableCode}");
            }

            var schemaObjectCode = variable.SchemaObjectCode?.Trim();
            if (!string.IsNullOrWhiteSpace(schemaObjectCode) && !domainObjectCodes.Contains(schemaObjectCode))
            {
                errors.Add($"{collectionName} {variableCode} 绑定的领域对象不存在: {schemaObjectCode}");
            }

            ValidateVariableFields(collectionName, variableCode, variable.Fields, errors);
        }
    }

    private static void ValidateVariableFields(
        string collectionName,
        string variableCode,
        IReadOnlyList<ApplicationMicroflowFieldDefinition> fields,
        List<string> errors)
    {
        var fieldCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            var fieldCode = field.FieldCode?.Trim();
            if (string.IsNullOrWhiteSpace(fieldCode))
            {
                errors.Add($"{collectionName} {variableCode} 字段编码不能为空");
                continue;
            }

            if (!fieldCodes.Add(fieldCode))
            {
                errors.Add($"{collectionName} {variableCode} 字段编码重复: {fieldCode}");
            }
        }
    }

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

    private static bool HasValueExpression(RuntimeValueExpressionDto? expression)
    {
        if (expression is null)
        {
            return false;
        }

        var kind = expression.Kind?.Trim();
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        if (kind.Equals("literal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (kind.Equals("ref", StringComparison.OrdinalIgnoreCase))
        {
            return expression.Ref is not null;
        }

        if (kind.Equals("function", StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(expression.FunctionId);
        }

        if (kind.Equals("object", StringComparison.OrdinalIgnoreCase))
        {
            return expression.Properties.Count > 0;
        }

        if (kind.Equals("array", StringComparison.OrdinalIgnoreCase) || kind.Equals("template", StringComparison.OrdinalIgnoreCase))
        {
            return expression.Items.Count > 0;
        }

        return false;
    }
}
