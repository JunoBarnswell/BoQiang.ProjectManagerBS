namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskParticipantUpsertRequest(string UserId, string? EmploymentId = null, string RoleCode = "Participant", long VersionNo = 0);
public sealed record ProjectManagementTaskParticipantResponse(string Id, string TaskId, string UserId, string? EmploymentId, string RoleCode, long VersionNo);
