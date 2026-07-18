namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementActivityEvent(
    string TenantId,
    string AppCode,
    string AggregateType,
    string AggregateId,
    string ActivityType,
    string? Summary,
    string TraceId,
    string ActorUserId,
    string? ProjectId = null);
