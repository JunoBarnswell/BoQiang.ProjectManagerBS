namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeExpressionFunctionParameterDto
{
    public string DataType { get; set; } = "string";

    public object? DefaultValue { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool Required { get; set; } = true;
}
