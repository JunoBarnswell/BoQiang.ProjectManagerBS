namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeModelFilterMappingDto
{
    public string Field { get; set; } = string.Empty;

    public string Operator { get; set; } = "equals";

    public RuntimeValueExpressionDto? ValueExpression { get; set; }

    public RuntimeValueExpressionDto? ValueToExpression { get; set; }
}
