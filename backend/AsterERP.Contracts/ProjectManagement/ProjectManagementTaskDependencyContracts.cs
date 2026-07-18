namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskDependencyUpsertRequest(
    string PredecessorTaskId,
    string SuccessorTaskId,
    string DependencyType = "FinishToStart",
    int LagMinutes = 0,
    long VersionNo = 0);

/// <summary>
/// 依赖导入使用同一份图校验：整个批次要么全部落库，要么全部失败。
/// 当前仅执行 Finish-to-Start 语义，保留 <see cref="ProjectManagementTaskDependencyUpsertRequest.DependencyType"/>
/// 是为了后续扩展其他依赖类型时不破坏 API 契约。
/// </summary>
public sealed record ProjectManagementTaskDependencyBatchCreateRequest(
    IReadOnlyList<ProjectManagementTaskDependencyUpsertRequest> Dependencies);

/// <summary>
/// 对存在未完成前置任务的任务进行强制开始。原因会被保存在任务状态说明和项目审计中。
/// </summary>
public sealed record ProjectManagementTaskDependencyForceStartRequest(
    string Reason,
    long VersionNo);

public sealed record ProjectManagementTaskDependencyResponse(
    string Id,
    string ProjectId,
    string PredecessorTaskId,
    string SuccessorTaskId,
    string DependencyType,
    int LagMinutes,
    long VersionNo);

public sealed record ProjectManagementTaskDependencyForceStartResponse(
    string TaskId,
    string ProjectId,
    string Status,
    int BlockingDependencyCount,
    string Reason,
    long VersionNo);
