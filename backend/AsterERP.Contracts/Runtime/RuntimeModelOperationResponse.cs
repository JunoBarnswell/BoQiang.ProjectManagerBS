namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeModelOperationResponse(
    string OperationCode,
    string OperationType,
    object? Result,
    IReadOnlyDictionary<string, object?> Variables);
