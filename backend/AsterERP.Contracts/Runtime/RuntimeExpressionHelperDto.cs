namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeExpressionHelperDto
{
    public string Name { get; set; } = string.Empty;

    public Dictionary<string, object?> Args { get; set; } = [];
}
