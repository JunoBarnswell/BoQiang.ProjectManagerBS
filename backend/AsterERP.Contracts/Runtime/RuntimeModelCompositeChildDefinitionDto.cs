namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeModelCompositeChildDefinitionDto
{
    public string ModelCode { get; set; } = string.Empty;

    public string ParentKeyField { get; set; } = "id";

    public string ForeignKeyField { get; set; } = string.Empty;

    public RuntimeValueExpressionDto? RowsExpression { get; set; }

    public RuntimeValueExpressionDto? DeleteIdsExpression { get; set; }

    public RuntimeValueExpressionDto? ParentIdExpression { get; set; }

    public List<RuntimeModelFieldMappingDto> FieldMappings { get; set; } = [];

    public bool DeleteMissing { get; set; }

    public bool Required { get; set; }
}
