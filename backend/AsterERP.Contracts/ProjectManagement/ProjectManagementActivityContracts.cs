namespace AsterERP.Contracts.ProjectManagement;

/// <summary>
/// 面向项目成员的业务时间线。它记录用户可理解的业务变化，不替代平台操作审计、登录审计或安全审计。
/// </summary>
public sealed record ProjectManagementActivityResponse(
    string Id,
    string ProjectId,
    string AggregateType,
    string AggregateId,
    string ActivityType,
    string? Summary,
    string TraceId,
    string ActorUserId,
    DateTime CreatedTime,
    string Source = "Business",
    IReadOnlyList<ProjectManagementActivityFieldChange>? FieldChanges = null,
    ProjectManagementActivityBatch? Batch = null,
    string? TargetRoute = null,
    bool IsTargetDeleted = false,
    string? ActorDisplayName = null);

public sealed record ProjectManagementActivityQuery(
    int PageIndex = 1,
    int PageSize = 50,
    string? AggregateType = null,
    string? AggregateId = null,
    string? ActivityType = null,
    string? ActorUserId = null,
    DateTime? From = null,
    DateTime? To = null);

/// <summary>
/// 单个业务字段的可读前后值。敏感字段的值必须在写入前脱敏，禁止将原值写入活动载荷。
/// </summary>
public sealed record ProjectManagementActivityFieldChange(
    string Field,
    string? DisplayName,
    string? Before,
    string? After,
    bool IsSensitive = false);

/// <summary>
/// 批量业务活动的聚合结果和有限明细。明细用于时间线展开，不承载平台级审计全量日志。
/// </summary>
public sealed record ProjectManagementActivityBatch(
    string OperationId,
    int TotalCount,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<ProjectManagementActivityBatchItem>? Details = null);

public sealed record ProjectManagementActivityBatchItem(
    string AggregateType,
    string AggregateId,
    string? Summary,
    IReadOnlyList<ProjectManagementActivityFieldChange>? FieldChanges = null);
