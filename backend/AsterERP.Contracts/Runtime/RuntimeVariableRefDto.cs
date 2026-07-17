namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeVariableRefDto
{
    public string VariableId { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string? SourceNodeId { get; set; }

    public string? OutputKey { get; set; }

    public List<string> FieldPath { get; set; } = [];

    public string DataType { get; set; } = "string";

    public string Label { get; set; } = string.Empty;
}
