namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementProjectQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Status = null);

public sealed record ProjectManagementProjectUpsertRequest(
    string ProjectCode,
    string ProjectName,
    string? Description = null,
    string Status = "Planning",
    string Priority = "Medium",
    string? OwnerUserId = null,
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    int? WipLimit = null,
    decimal ProgressPercent = 0,
    long VersionNo = 0);

public sealed record ProjectManagementProjectResponse(
    string Id,
    string TenantId,
    string AppCode,
    string ProjectCode,
    string ProjectName,
    string? Description,
    string Status,
    string Priority,
    string OwnerUserId,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime? CompletedAt,
    int? WipLimit,
    decimal ProgressPercent,
    long VersionNo,
    DateTime CreatedTime,
    DateTime? UpdatedTime);
