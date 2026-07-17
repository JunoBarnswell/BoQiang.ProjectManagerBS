using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowCompositeChildDeleteDefinition
{
    public string ModelCode { get; set; } = string.Empty;

    public string ParentKeyField { get; set; } = "id";

    public string ForeignKeyField { get; set; } = string.Empty;

    public RuntimeValueExpressionDto? ParentIdExpression { get; set; }

    public bool Required { get; set; }
}
