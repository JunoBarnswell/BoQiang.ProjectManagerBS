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
    string? TraceId = null,
    string? TenantId = null,
    string? AppCode = null,
    long AggregateVersion = 0,
    long WorkspaceSequence = 0,
    long ProjectSequence = 0,
    IReadOnlyDictionary<string, object?>? Patch = null,
    string? ClientMutationId = null);
