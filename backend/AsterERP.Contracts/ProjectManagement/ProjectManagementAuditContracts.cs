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
    DateTime? To = null);

public sealed record ProjectManagementAuditItem(
    string Id,
    string ProjectId,
    string AggregateType,
    string AggregateId,
    string ActivityType,
    string? Summary,
    string TraceId,
    string ActorUserId,
    DateTime CreatedTime);

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
    string ImpactJson,
    string? ErrorMessage,
    string TraceId,
    string ActorUserId,
    DateTime StartedTime,
    DateTime? CompletedTime);
