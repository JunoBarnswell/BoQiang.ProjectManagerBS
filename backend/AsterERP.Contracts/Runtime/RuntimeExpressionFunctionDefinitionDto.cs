namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeExpressionFunctionDefinitionDto
{
    public string CanonicalName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string DisabledReason { get; set; } = string.Empty;

    public bool Deterministic { get; set; } = true;

    public List<string> Examples { get; set; } = [];

    public string FunctionName { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string ModuleKey { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string Namespace { get; set; } = string.Empty;

    public List<RuntimeExpressionFunctionParameterDto> Parameters { get; set; } = [];

    public string QualifiedName { get; set; } = string.Empty;

    public bool RequiresInput { get; set; } = true;

    public string ReturnType { get; set; } = "string";

    public bool SqlEnabled { get; set; } = true;
}
