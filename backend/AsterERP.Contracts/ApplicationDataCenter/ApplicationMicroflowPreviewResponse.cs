namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowPreviewResponse(
    string FlowCode,
    string Mode,
    string Message,
    string? PrimaryDatasetKey,
    IReadOnlyList<ApplicationMicroflowPreviewDatasetResponse> Datasets,
    IReadOnlyList<ApplicationMicroflowPreviewTraceItemResponse> Trace,
    IReadOnlyList<ApplicationMicroflowPreviewVariableResponse> Variables,
    object? RawResult);
