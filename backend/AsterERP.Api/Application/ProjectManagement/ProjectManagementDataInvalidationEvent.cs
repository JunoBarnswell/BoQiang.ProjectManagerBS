namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementDataInvalidationEvent(
    string TenantId,
    string AppCode,
    string AggregateType,
    string AggregateId,
    string EventType,
    long Version,
    string TraceId,
    string? ProjectId = null,
    string? EventId = null,
    IReadOnlyList<string>? ChangedFields = null,
    IReadOnlyDictionary<string, object?>? Patch = null,
    string? ClientMutationId = null,
    IReadOnlyCollection<string>? AdditionalHomeUserIds = null);
