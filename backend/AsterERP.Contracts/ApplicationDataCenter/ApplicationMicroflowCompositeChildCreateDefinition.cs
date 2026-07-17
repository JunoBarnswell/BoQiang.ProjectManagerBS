using AsterERP.Contracts.Runtime;

namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowCompositeChildCreateDefinition
{
    public string ModelCode { get; set; } = string.Empty;

    public string ParentKeyField { get; set; } = "id";

    public string ForeignKeyField { get; set; } = string.Empty;

    public RuntimeValueExpressionDto? RowsExpression { get; set; }

    public List<ApplicationMicroflowDataMappingDefinition> FieldMappings { get; set; } = [];
}
