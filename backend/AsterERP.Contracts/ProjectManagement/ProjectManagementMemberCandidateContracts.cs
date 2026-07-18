using AsterERP.Shared;

namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementMemberCandidateResponse(
    string UserId,
    string UserName,
    string DisplayName,
    string? DeptId,
    string? DeptName,
    string? PositionId,
    string? PositionName,
    string EmploymentId,
    string EmploymentName,
    string Status,
    bool IsSelectable);

public sealed record ProjectManagementMemberCandidateQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? DeptId = null,
    string? PositionId = null);
