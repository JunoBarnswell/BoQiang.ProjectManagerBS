namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementDataInvalidationEvent(
    string TenantId,
    string AppCode,
    string AggregateType,
    string AggregateId,
    string EventType,
    long Version,
    string TraceId,
    string? ProjectId = null);
