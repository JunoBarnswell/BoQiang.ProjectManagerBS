namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record FlowiseGraphNodeExecution(
    string NodeId,
    FlowiseRuntimeNodeKind Kind,
    string Status,
    object? Output = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);
