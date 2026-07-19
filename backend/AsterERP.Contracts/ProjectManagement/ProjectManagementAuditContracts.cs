using AsterERP.Shared;

namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementAuditQuery(
    int PageIndex = 1,
    int PageSize = 50,
    string? ProjectId = null,
    string? AggregateType = null,
    string? ActivityType = null,
    string? Keyword = null,
    DateTime? From = null,
    DateTime? To = null,
    string? ActorUserId = null,
    string? ActorRole = null,
    string? Source = null,
    string? SourceDeviceId = null,
    bool? IsSuccess = null,
    List<GridSort>? Sorts = null);

public sealed record ProjectManagementAuditItem(
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
    string? SourceDeviceId = null,
    bool IsSuccess = true);

/// <summary>
/// 单条审计记录的受控详情。不会暴露 pm_activities.Remark 原文，字段差异会在服务端再次脱敏。
/// </summary>
public sealed record ProjectManagementAuditDetail(
    ProjectManagementAuditItem Audit,
    IReadOnlyList<ProjectManagementActivityFieldChange> FieldChanges,
    ProjectManagementActivityBatch? Batch,
    ProjectManagementAuditEntitySnapshot EntitySnapshot,
    string? FailureReason,
    IReadOnlyList<ProjectManagementAuditRelatedEvent> RelatedEvents,
    IReadOnlyList<ProjectManagementAuditReference> References,
    string? TraceDiagnosticsRoute);

/// <summary>
/// 以审计行自身的不可变标识构成的历史快照；目标实体已删除时仍可用于定位当时的操作对象。
/// </summary>
public sealed record ProjectManagementAuditEntitySnapshot(
    string ProjectId,
    string AggregateType,
    string AggregateId,
    string? Summary,
    bool IsDeleted);

/// <summary>
/// 同一 Trace 中按发生时间排序的事件。Causality 描述它与当前审计行的因果位置。
/// </summary>
public sealed record ProjectManagementAuditRelatedEvent(
    string Id,
    string Kind,
    string Causality,
    string? AggregateType,
    string? AggregateId,
    string? ActivityType,
    string? Summary,
    string? Status,
    DateTime OccurredAt);

/// <summary>
/// 从审计上下文提取的同步、导入、备份、工作流或操作任务标识。
/// </summary>
public sealed record ProjectManagementAuditReference(
    string Kind,
    string Id,
    string? DisplayName = null);

public sealed record ProjectManagementAuditExportResponse(
    string FileName,
    byte[] Content,
    int Count);

public sealed record ProjectManagementOperationQuery(
    int PageIndex = 1,
    int PageSize = 50,
    string? OperationType = null,
    string? Status = null);

public sealed record ProjectManagementOperationItem(
    string Id,
    string OperationType,
    string Status,
    string Phase,
    int ProgressPercent,
    bool IsCancellationRequested,
    string ImpactJson,
    string? ErrorMessage,
    string TraceId,
    string ActorUserId,
    DateTime StartedTime,
    DateTime? CompletedTime);
