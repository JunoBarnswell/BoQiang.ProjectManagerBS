namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementRealtimeEvent(
    string AggregateType,
    string AggregateId,
    string EventType,
    long Version,
    string ProjectId,
    string? EventId = null,
    long Sequence = 0,
    IReadOnlyList<string>? ChangedFields = null,
    string? TraceId = null);
