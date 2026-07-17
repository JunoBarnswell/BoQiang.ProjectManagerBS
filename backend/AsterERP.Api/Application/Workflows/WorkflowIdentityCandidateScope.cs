using AsterERP.Api.Infrastructure.Workflows;
using System.Diagnostics.CodeAnalysis;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowIdentityCandidateScope
{
    string CurrentUserId { get; }

    IReadOnlyList<string> CandidateUserIds { get; }

    IReadOnlyList<string> CandidateRoleIds { get; }

    IReadOnlyList<string> CandidateRoleGroupIds { get; }

    IReadOnlyList<string> CandidateDepartmentGroupIds { get; }

    IReadOnlyList<string> CandidatePositionGroupIds { get; }

    IReadOnlyList<string> CandidateGroupIds { get; }

    bool IsCandidateUser(string? userId);

    bool IsCandidateGroup(string? groupId);
}

public sealed class WorkflowIdentityCandidateScope(IWorkflowIdentityScopeResolver scopeResolver) : IWorkflowIdentityCandidateScope
{
    public string CurrentUserId => scopeResolver.UserId;

    public IReadOnlyList<string> CandidateUserIds => BuildCandidateUserIds();

    public IReadOnlyList<string> CandidateRoleIds => scopeResolver.RoleIds;

    public IReadOnlyList<string> CandidateRoleGroupIds => BuildGroupIds(scopeResolver.RoleIds, WorkflowIdentityKeys.RoleGroup);

    public IReadOnlyList<string> CandidateDepartmentGroupIds => BuildGroupIds(scopeResolver.DeptIds, WorkflowIdentityKeys.DepartmentGroup);

    public IReadOnlyList<string> CandidatePositionGroupIds => BuildGroupIds(scopeResolver.PositionIds, WorkflowIdentityKeys.PositionGroup);

    public IReadOnlyList<string> CandidateGroupIds => BuildCandidateGroupIds();

    public bool IsCandidateUser([NotNullWhen(true)] string? userId) =>
        !string.IsNullOrWhiteSpace(userId) && CandidateUserIds.Contains(userId, StringComparer.OrdinalIgnoreCase);

    public bool IsCandidateGroup([NotNullWhen(true)] string? groupId) =>
        !string.IsNullOrWhiteSpace(groupId) && CandidateGroupIds.Contains(groupId, StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<string> BuildCandidateUserIds()
    {
        if (string.IsNullOrWhiteSpace(CurrentUserId))
        {
            return [];
        }

        return [CurrentUserId.Trim()];
    }

    private IReadOnlyList<string> BuildCandidateGroupIds()
    {
        var groupIds = CandidateRoleGroupIds.ToList();

        groupIds.AddRange(CandidateDepartmentGroupIds);
        groupIds.AddRange(CandidatePositionGroupIds);

        return groupIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildGroupIds(IEnumerable<string> sourceIds, Func<string, string> groupIdFactory)
    {
        return sourceIds
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => groupIdFactory(item.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

}
