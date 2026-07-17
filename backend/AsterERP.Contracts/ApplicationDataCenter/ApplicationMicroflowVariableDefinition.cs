namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowVariableDefinition
{
    public string VariableCode { get; set; } = string.Empty;

    public string VariableName { get; set; } = string.Empty;

    public string ValueType { get; set; } = "string";

    public object? DefaultValue { get; set; }

    public string? SchemaObjectCode { get; set; }

    public string? SourceNodeId { get; set; }

    public List<ApplicationMicroflowFieldDefinition> Fields { get; set; } = [];
}
