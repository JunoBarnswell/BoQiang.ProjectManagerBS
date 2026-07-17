namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeExpressionFunctionCatalogResponse
{
    public List<RuntimeExpressionFunctionDefinitionDto> Functions { get; set; } = [];

    public string Scope { get; set; } = string.Empty;
}
