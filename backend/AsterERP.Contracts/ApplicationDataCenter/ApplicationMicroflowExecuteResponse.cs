namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowExecuteResponse(
    string FlowCode,
    object? Result,
    Dictionary<string, object?> Variables,
    IReadOnlyList<string> Trace);
