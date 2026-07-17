using AsterERP.Contracts.Expressions;

namespace AsterERP.Contracts.Runtime;

public sealed class RuntimeValueExpressionDto
{
    public string Version { get; set; } = "latest";

    public string Kind { get; set; } = "literal";

    public string DataType { get; set; } = "string";

    public object? Value { get; set; }

    public string? ResourceId { get; set; }

    public RuntimeValueExpressionDto? Input { get; set; }

    public RuntimeValueExpressionDto? When { get; set; }

    public RuntimeValueExpressionDto? Then { get; set; }

    public RuntimeValueExpressionDto? Otherwise { get; set; }

    public string? Operator { get; set; }

    public object? Fallback { get; set; }

    public List<ExpressionConversionStepDto> Pipeline { get; set; } = [];

    public IReadOnlyList<string> Dependencies { get; set; } = [];

    public string? CanonicalHash { get; set; }

    public RuntimeVariableRefDto? Ref { get; set; }

    public string? FunctionId { get; set; }

    public List<RuntimeValueExpressionDto> Args { get; set; } = [];

    public List<RuntimeValueExpressionDto> Items { get; set; } = [];

    public Dictionary<string, RuntimeValueExpressionDto> Properties { get; set; } = [];
}
