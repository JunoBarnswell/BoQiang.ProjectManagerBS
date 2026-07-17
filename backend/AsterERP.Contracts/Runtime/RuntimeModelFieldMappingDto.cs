namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeModelFieldMappingDto
{
    public string TargetField { get; set; } = string.Empty;

    public RuntimeValueExpressionDto? Expression { get; set; }
}
