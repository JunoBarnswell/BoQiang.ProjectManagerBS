namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementTaskParticipantUpsertRequest(string UserId, string? EmploymentId = null, string RoleCode = "Participant", long VersionNo = 0);
public sealed record ProjectManagementTaskParticipantResponse(
    string Id,
    string TaskId,
    string UserId,
    string? EmploymentId,
    string RoleCode,
    long VersionNo,
    bool IsCurrentAssignment = true,
    bool IsProjectMemberActive = true);

/// <summary>
/// 仅用于任务参与人分配的项目成员候选项。候选项已限定为当前项目中仍启用且身份有效的成员。
/// </summary>
public sealed record ProjectManagementTaskParticipantCandidateResponse(
    string UserId,
    string? EmploymentId,
    string RoleCode,
    string? ScopeRootTaskId,
    string UserName,
    string DisplayName);

public sealed record ProjectManagementTaskParticipantCandidateQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? Keyword = null);
