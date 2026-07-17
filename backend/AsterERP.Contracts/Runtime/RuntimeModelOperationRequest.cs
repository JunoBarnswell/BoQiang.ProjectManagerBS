namespace AsterERP.Contracts.Runtime;

public sealed record RuntimeModelOperationRequest(
    string OperationCode,
    IReadOnlyDictionary<string, object?>? Variables,
    string? PageCode = null,
    string? PreviewPageId = null);
