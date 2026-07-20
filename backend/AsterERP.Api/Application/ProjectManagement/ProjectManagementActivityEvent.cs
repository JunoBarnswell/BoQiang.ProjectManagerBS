namespace AsterERP.Api.Application.ProjectManagement;

using AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementActivityEvent(
    string TenantId,
    string AppCode,
    string AggregateType,
    string AggregateId,
    string ActivityType,
    string? Summary,
    string TraceId,
    string ActorUserId,
    string? ProjectId = null,
    string Source = "Business",
    IReadOnlyList<ProjectManagementActivityFieldChange>? FieldChanges = null,
    ProjectManagementActivityBatch? Batch = null,
    DateTime? OccurredAt = null,
    ProjectManagementLocalizedText? SummaryText = null);
