using System.Text.Json;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationMicroflowOutputSchemaSynchronizer
{
    public ApplicationMicroflowDefinition Synchronize(ApplicationMicroflowDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var outputs = new List<ApplicationMicroflowVariableDefinition>(definition.Outputs);
        foreach (var node in definition.Nodes.Where(item => string.Equals(item.Type, "return", StringComparison.OrdinalIgnoreCase)))
        {
            var schema = ReadOutputSchema(node);
            if (schema is null || string.IsNullOrWhiteSpace(schema.VariableCode))
            {
                continue;
            }

            var output = CreateOutput(node, schema);
            var existingIndex = outputs.FindIndex(item => string.Equals(item.VariableCode, output.VariableCode, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                outputs[existingIndex] = output;
            }
            else
            {
                outputs.Add(output);
            }
        }

        definition.Outputs = outputs;
        return definition;
    }

    public string SynchronizeJson(string configJson)
    {
        var definition = ApplicationMicroflowDefinitionReader.Read(configJson);
        return ApplicationDataCenterJson.Serialize(Synchronize(definition));
    }

    private static ApplicationMicroflowVariableDefinition CreateOutput(
        ApplicationMicroflowNodeDefinition node,
        ApplicationMicroflowOutputSchemaDefinition schema) =>
        new()
        {
            DefaultValue = CreateDefaultValue(schema.ValueType),
            Fields = CloneFields(schema.Fields),
            SchemaObjectCode = null,
            SourceNodeId = node.Id,
            ValueType = string.IsNullOrWhiteSpace(schema.ValueType) ? "object" : schema.ValueType.Trim(),
            VariableCode = schema.VariableCode.Trim(),
            VariableName = string.IsNullOrWhiteSpace(schema.VariableName) ? schema.VariableCode.Trim() : schema.VariableName.Trim()
        };

    private static object? CreateDefaultValue(string? valueType)
    {
        if (string.Equals(valueType, "array", StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<object>();
        }

        if (string.Equals(valueType, "object", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(valueType, "json", StringComparison.OrdinalIgnoreCase))
        {
            return new Dictionary<string, object?>();
        }

        return null;
    }

    private static List<ApplicationMicroflowFieldDefinition> CloneFields(IReadOnlyCollection<ApplicationMicroflowFieldDefinition> fields) =>
        fields
            .Select(field => Clone(field))
            .ToList();

    private static ApplicationMicroflowFieldDefinition Clone(ApplicationMicroflowFieldDefinition field) =>
        JsonSerializer.Deserialize<ApplicationMicroflowFieldDefinition>(
            JsonSerializer.Serialize(field, ApplicationDataCenterJson.Options),
            ApplicationDataCenterJson.Options)
        ?? throw new ValidationException("Return 输出字段同步失败", ErrorCodes.ApplicationDataCenterInvalidConfig);

    private static ApplicationMicroflowOutputSchemaDefinition? ReadOutputSchema(ApplicationMicroflowNodeDefinition node) =>
        ReadJson<ApplicationMicroflowOutputSchemaDefinition>(node.Config, "outputSchema");

    private static T? ReadJson<T>(IReadOnlyDictionary<string, object?> config, string key)
    {
        if (!config.TryGetValue(key, out var raw) || raw is null)
        {
            return default;
        }

        if (raw is JsonElement element)
        {
            return element.Deserialize<T>(ApplicationDataCenterJson.Options);
        }

        return JsonSerializer.Deserialize<T>(
            JsonSerializer.Serialize(raw, ApplicationDataCenterJson.Options),
            ApplicationDataCenterJson.Options);
    }
}
