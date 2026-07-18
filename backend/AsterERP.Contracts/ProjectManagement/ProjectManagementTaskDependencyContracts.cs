namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskDependencyUpsertRequest(
    string PredecessorTaskId,
    string SuccessorTaskId,
    string DependencyType = "FinishToStart",
    int LagMinutes = 0,
    long VersionNo = 0);

public sealed record ProjectManagementTaskDependencyResponse(
    string Id,
    string ProjectId,
    string PredecessorTaskId,
    string SuccessorTaskId,
    string DependencyType,
    int LagMinutes,
    long VersionNo);
