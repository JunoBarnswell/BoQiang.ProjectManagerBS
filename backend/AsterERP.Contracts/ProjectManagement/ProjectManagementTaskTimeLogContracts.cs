namespace AsterERP.Contracts.ProjectManagement;

/// <summary>
/// 时间日志的唯一计量单位为整分钟。服务端根据起止时间计算分钟数，客户端不得传入浮点小时数。
/// </summary>
public sealed record ProjectManagementTaskTimeLogUpsertRequest(DateTime StartedAt, DateTime EndedAt, string? Note = null, long VersionNo = 0);

/// <summary>
/// 更新日志时同时校验日志和所属任务版本，避免覆盖并发的工时汇总。
/// </summary>
public sealed record ProjectManagementTaskTimeLogUpdateRequest(
    DateTime StartedAt,
    DateTime EndedAt,
    string? Note,
    long VersionNo,
    long TaskVersionNo);

public sealed record ProjectManagementTaskTimeLogResponse(string Id, string TaskId, string UserId, DateTime StartedAt, DateTime EndedAt, int Minutes, string? Note, long VersionNo);

/// <summary>
/// 人员工作量按项目聚合。任务数量和预计分钟归属任务负责人；已登记分钟归属实际填写时间日志的人员。
/// 所有工时字段均为整分钟。
/// </summary>
public sealed record ProjectManagementTaskWorkloadQuery(
    string ProjectId,
    DateTime? TimeLogStartedFrom = null,
    DateTime? TimeLogStartedTo = null);

public sealed record ProjectManagementTaskWorkloadResponse(
    string UserId,
    int TodoTaskCount,
    int InProgressTaskCount,
    int CompletedTaskCount,
    int OverdueTaskCount,
    int EstimatedMinutes,
    int LoggedMinutes);
