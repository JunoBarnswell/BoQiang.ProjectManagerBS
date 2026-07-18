namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementMilestoneResponse(
    string Id,
    string ProjectId,
    string MilestoneName,
    string? Description,
    string? OwnerUserId,
    string Status,
    string HealthStatus,
    DateTime? StartDate,
    DateTime? DueDate,
    DateTime? CompletedAt,
    decimal ProgressPercent,
    int SortOrder,
    long VersionNo);

public sealed record ProjectManagementMilestoneUpsertRequest(
    string MilestoneName,
    string? Description = null,
    string? OwnerUserId = null,
    string Status = "Planned",
    DateTime? StartDate = null,
    DateTime? DueDate = null,
    decimal ProgressPercent = 0,
    int SortOrder = 0,
    long VersionNo = 0);
