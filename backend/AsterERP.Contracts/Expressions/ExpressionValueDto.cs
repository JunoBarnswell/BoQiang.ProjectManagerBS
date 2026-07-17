namespace AsterERP.Contracts.Expressions;

public sealed class ExpressionValueDto
{
    public string Version { get; set; } = "latest";

    public string Kind { get; set; } = "literal";

    public string DataType { get; set; } = "json";

    public object? Value { get; set; }

    public string? ResourceId { get; set; }

    public string? FunctionId { get; set; }

    public List<ExpressionValueDto> Args { get; set; } = [];

    public ExpressionValueDto? Input { get; set; }

    public ExpressionValueDto? When { get; set; }

    public ExpressionValueDto? Then { get; set; }

    public ExpressionValueDto? Otherwise { get; set; }

    public string? Operator { get; set; }

    public Dictionary<string, ExpressionValueDto> Properties { get; set; } = [];

    public List<ExpressionValueDto> Items { get; set; } = [];

    public List<ExpressionConversionStepDto> Pipeline { get; set; } = [];

    public object? Fallback { get; set; }

    public IReadOnlyList<string> Dependencies { get; set; } = [];

    public string? CanonicalHash { get; set; }
}
