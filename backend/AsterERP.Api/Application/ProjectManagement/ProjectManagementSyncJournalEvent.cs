using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementSyncJournalEvent(
    string TenantId,
    string AppCode,
    string AggregateType,
    string AggregateId,
    string? ProjectId,
    string Operation,
    long VersionNo,
    string PayloadJson,
    string ActorUserId,
    string? DeviceId,
    string TraceId,
    string Source = "User",
    string? PreviousPayloadJson = null,
    IReadOnlyList<ProjectManagementSyncFieldChange>? FieldChanges = null);
