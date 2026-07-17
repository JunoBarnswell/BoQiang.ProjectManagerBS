namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeModelOperationDefinitionDto
{
    public string OperationCode { get; set; } = string.Empty;

    public string OperationName { get; set; } = string.Empty;

    public string OperationType { get; set; } = "query";

    public string? ModelCode { get; set; }

    public RuntimeValueExpressionDto? IdExpression { get; set; }

    public List<RuntimeModelFieldMappingDto> FieldMappings { get; set; } = [];

    public List<RuntimeModelFilterMappingDto> Filters { get; set; } = [];

    public List<RuntimeModelCompositeChildDefinitionDto> Children { get; set; } = [];

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
