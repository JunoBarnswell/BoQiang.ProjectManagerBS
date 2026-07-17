namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowExecuteRequest(
    Dictionary<string, object?>? Variables = null,
    string? StartNodeId = null,
    string? PageCode = null,
    string? PreviewPageId = null,
    string? ModelCode = null,
    string? Action = null,
    string? BindingId = null,
    string? CorrelationId = null,
    int? TimeoutMs = null);
