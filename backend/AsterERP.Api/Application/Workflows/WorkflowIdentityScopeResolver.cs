using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Infrastructure.Workflows;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowIdentityScopeResolver
{
    string UserId { get; }

    string UserName { get; }

    string? TenantId { get; }

    string? AppCode { get; }

    string? EmploymentId { get; }

    string? DeptId { get; }

    string? PositionId { get; }

    IReadOnlyList<string> DeptIds { get; }

    IReadOnlyList<string> PositionIds { get; }

    IReadOnlyList<string> RoleIds { get; }

    IReadOnlyList<string> CandidateGroupIds { get; }

    bool IsAuthenticated { get; }
}

public sealed class WorkflowIdentityScopeResolver(ICurrentUser currentUser) : IWorkflowIdentityScopeResolver
{
    public string UserId => currentUser.GetAsterErpUserId();

    public string UserName => currentUser.UserName ?? string.Empty;

    public string? TenantId => currentUser.GetAsterErpTenantId();

    public string? AppCode => currentUser.GetAsterErpAppCode();

    public string? EmploymentId => currentUser.GetAsterErpEmploymentId();

    public string? DeptId => currentUser.GetAsterErpDeptId();

    public string? PositionId => currentUser.GetAsterErpPositionId();

    public IReadOnlyList<string> DeptIds => currentUser.GetAsterErpDeptIds();

    public IReadOnlyList<string> PositionIds => currentUser.GetAsterErpPositionIds();

    public IReadOnlyList<string> RoleIds => currentUser.GetAsterErpRoleIds();

    public IReadOnlyList<string> CandidateGroupIds => BuildCandidateGroupIds();

    public bool IsAuthenticated => currentUser.IsAsterErpAuthenticated();

    private IReadOnlyList<string> BuildCandidateGroupIds()
    {
        var groupIds = RoleIds.Select(WorkflowIdentityKeys.RoleGroup).ToList();
        groupIds.AddRange(DeptIds.Select(WorkflowIdentityKeys.DepartmentGroup));

        groupIds.AddRange(PositionIds.Select(WorkflowIdentityKeys.PositionGroup));

        return groupIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
