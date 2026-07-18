namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementRealtimeEvent(
    string AggregateType,
    string AggregateId,
    string EventType,
    long Version,
    string ProjectId);
