namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementOperationResponse(
    string Id,
    string OperationType,
    string Status,
    string Phase,
    int ProgressPercent,
    bool IsCancellationRequested,
    string ImpactJson,
    string? ErrorMessage,
    string TraceId,
    DateTime StartedTime,
    DateTime? CompletedTime);

public sealed record ProjectManagementOperationProgressEvent(
    string Id,
    string OperationType,
    string Status,
    string Phase,
    int ProgressPercent,
    bool IsCancellationRequested,
    DateTime? CompletedTime);
