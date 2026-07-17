using System.Text.Json;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

internal static class ApplicationMicroflowGlobalVariableNodeReader
{
    public const string NodeType = "globalVariables";

    public static bool IsGlobalVariableNode(ApplicationMicroflowNodeDefinition node) =>
        string.Equals(node.Type, NodeType, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<ApplicationMicroflowVariableDefinition> ReadVariables(ApplicationMicroflowDefinition definition) =>
        definition.Nodes
            .Where(IsGlobalVariableNode)
            .SelectMany(ReadVariables)
            .ToList();

    public static IReadOnlyList<ApplicationMicroflowVariableDefinition> ReadVariables(ApplicationMicroflowNodeDefinition node)
    {
        if (!node.Config.TryGetValue("variables", out var value) || value is null)
        {
            return [];
        }

        var variables = value is JsonElement element
            ? JsonSerializer.Deserialize<List<ApplicationMicroflowVariableDefinition>>(element.GetRawText(), ApplicationDataCenterJson.Options)
            : JsonSerializer.Deserialize<List<ApplicationMicroflowVariableDefinition>>(JsonSerializer.Serialize(value, ApplicationDataCenterJson.Options), ApplicationDataCenterJson.Options);

        return variables?
            .Select(variable => NormalizeVariable(variable, node.Id))
            .ToList() ?? [];
    }

    private static ApplicationMicroflowVariableDefinition NormalizeVariable(ApplicationMicroflowVariableDefinition variable, string nodeId)
    {
        variable.SourceNodeId = nodeId;
        variable.VariableCode = variable.VariableCode?.Trim() ?? string.Empty;
        variable.VariableName = string.IsNullOrWhiteSpace(variable.VariableName)
            ? variable.VariableCode
            : variable.VariableName.Trim();
        variable.ValueType = string.IsNullOrWhiteSpace(variable.ValueType)
            ? "string"
            : variable.ValueType.Trim();
        variable.SchemaObjectCode = string.IsNullOrWhiteSpace(variable.SchemaObjectCode)
            ? null
            : variable.SchemaObjectCode.Trim();
        variable.Fields ??= [];
        return variable;
    }
}
