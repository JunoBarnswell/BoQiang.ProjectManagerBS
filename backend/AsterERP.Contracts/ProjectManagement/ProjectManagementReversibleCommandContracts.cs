namespace AsterERP.Contracts.ProjectManagement;

/// <summary>
/// 可撤销业务命令的受支持类型。载荷必须是能够再次进入业务服务的最小命令，不能是数据库快照。
/// </summary>
public static class ProjectManagementReversibleCommandTypes
{
    public const string TaskUpdated = "task.updated";
    public const string TaskStatusProgressChanged = "task.status-progress.changed";
    public const string TaskAssigneeChanged = "task.assignee.changed";
    public const string TaskParticipantChanged = "task.participant.changed";
    public const string TaskLabelsChanged = "task.labels.changed";
    public const string TaskSoftDeleted = "task.soft-deleted";
    public const string TaskRestored = "task.restored";
    public const string ProjectUpdated = "project.updated";
    public const string ProjectSoftDeleted = "project.soft-deleted";
    public const string ProjectRestored = "project.restored";

    public static readonly IReadOnlySet<string> Supported = new HashSet<string>(StringComparer.Ordinal)
    {
        TaskUpdated,
        TaskStatusProgressChanged,
        TaskAssigneeChanged,
        TaskParticipantChanged,
        TaskLabelsChanged,
        TaskSoftDeleted,
        TaskRestored,
        ProjectUpdated,
        ProjectSoftDeleted,
        ProjectRestored
    };
}

public static class ProjectManagementReversibleCommandStates
{
    public const string Applied = "Applied";
    public const string Undone = "Undone";
    public const string Invalidated = "Invalidated";
}

public static class ProjectManagementReversibleCommandDirections
{
    public const string Undo = "Undo";
    public const string Redo = "Redo";
}

/// <summary>写入后的原始业务命令，用于命令服务在事务提交后登记撤销账本。</summary>
public sealed record ProjectManagementReversibleCommandRecordRequest(
    string OriginRequestId,
    string CommandType,
    string ProjectId,
    string AggregateType,
    string AggregateId,
    string ForwardCommandJson,
    string InverseCommandJson,
    string TraceId,
    string? Summary = null);

public sealed record ProjectManagementReversibleCommandExecuteRequest(string RequestId);

public sealed record ProjectManagementReversibleCommandResponse(
    string Id,
    long SequenceNo,
    string CommandType,
    string ProjectId,
    string AggregateType,
    string AggregateId,
    string State,
    string? Summary,
    string TraceId,
    DateTime CreatedTime,
    DateTime? LastReplayedTime,
    bool IsReplayPending);

public sealed record ProjectManagementReversibleCommandStackResponse(
    IReadOnlyList<ProjectManagementReversibleCommandResponse> Commands,
    bool CanUndo,
    bool CanRedo);
