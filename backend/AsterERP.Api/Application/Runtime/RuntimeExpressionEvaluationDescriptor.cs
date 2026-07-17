namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeExpressionEvaluationDescriptor
{
    public string? BindingKey { get; init; }

    public string? ExpressionName { get; init; }

    public string? ModelCode { get; init; }

    public string? OwnerId { get; init; }

    public string? OwnerName { get; init; }

    public string? OwnerType { get; init; }
}
