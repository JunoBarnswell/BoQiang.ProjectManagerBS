namespace AsterERP.Contracts.Expressions;

public sealed class ExpressionConversionStepDto
{
    public string From { get; set; } = "json";

    public string Name { get; set; } = string.Empty;

    public string To { get; set; } = "json";
}
