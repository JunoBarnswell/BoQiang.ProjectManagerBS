using AsterERP.Shared;

namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementMemberResponse(
    string Id,
    string ProjectId,
    string UserId,
    string? EmploymentId,
    string RoleCode,
    string? ScopeRootTaskId,
    bool IsActive,
    DateTime JoinedAt,
    DateTime? LeftAt,
    long VersionNo,
    string? DisplayName = null);

public sealed record ProjectManagementMemberUpsertRequest(
    string UserId,
    string? EmploymentId = null,
    string RoleCode = "Member",
    string? ScopeRootTaskId = null,
    long VersionNo = 0);
