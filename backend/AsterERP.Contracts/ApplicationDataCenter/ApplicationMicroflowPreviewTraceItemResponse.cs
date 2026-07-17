namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowPreviewTraceItemResponse(
    int Order,
    string NodeId,
    string NodeName,
    string NodeType);
