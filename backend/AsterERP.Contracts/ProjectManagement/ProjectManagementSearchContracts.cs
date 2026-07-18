namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementSearchQuery(
    string Keyword,
    string Scope = "all",
    int Limit = 20,
    string? ProjectId = null,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    int PageIndex = 1);

public sealed record ProjectManagementSearchItem(string ResultType, string Id, string ProjectId, string Title, string? Summary, string TargetRoute, DateTime? UpdatedTime);

public sealed record ProjectManagementSearchResponse(
    IReadOnlyList<ProjectManagementSearchItem> Projects,
    IReadOnlyList<ProjectManagementSearchItem> Tasks,
    IReadOnlyList<ProjectManagementSearchItem> Milestones,
    IReadOnlyList<ProjectManagementSearchItem> Labels,
    IReadOnlyList<ProjectManagementSearchItem> Members,
    IReadOnlyList<ProjectManagementSearchItem> Comments);

public sealed record ProjectManagementSearchIndexOperationRequest(int BatchSize = 200);

public sealed record ProjectManagementSearchIndexOperationResponse(
    string OperationId,
    string OperationType);

public sealed record ProjectManagementSearchIndexStatusResponse(
    string Status,
    string Mode,
    long AppliedSequenceNo,
    long TargetSequenceNo,
    int DocumentCount,
    int FailureCount,
    string? LastError,
    string? OperationId,
    DateTime? StartedTime,
    DateTime? CompletedTime,
    DateTime UpdatedTime);
