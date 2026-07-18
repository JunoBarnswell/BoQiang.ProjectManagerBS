namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskTimeLogUpsertRequest(DateTime StartedAt, DateTime EndedAt, string? Note = null, long VersionNo = 0);
public sealed record ProjectManagementTaskTimeLogResponse(string Id, string TaskId, string UserId, DateTime StartedAt, DateTime EndedAt, int Minutes, string? Note, long VersionNo);
