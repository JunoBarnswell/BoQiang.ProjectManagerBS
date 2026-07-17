using Volo.Abp.Users;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowCurrentUserContext
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

public sealed class WorkflowCurrentUserContext : IWorkflowCurrentUserContext
{
    private readonly IWorkflowIdentityScopeResolver _scopeResolver;

    public WorkflowCurrentUserContext(IWorkflowIdentityScopeResolver scopeResolver)
    {
        _scopeResolver = scopeResolver;
    }

    public string UserId => _scopeResolver.UserId;

    public string UserName => _scopeResolver.UserName;

    public string? TenantId => _scopeResolver.TenantId;

    public string? AppCode => _scopeResolver.AppCode;

    public string? EmploymentId => _scopeResolver.EmploymentId;

    public string? DeptId => _scopeResolver.DeptId;

    public string? PositionId => _scopeResolver.PositionId;

    public IReadOnlyList<string> DeptIds => _scopeResolver.DeptIds;

    public IReadOnlyList<string> PositionIds => _scopeResolver.PositionIds;

    public IReadOnlyList<string> RoleIds => _scopeResolver.RoleIds;

    public IReadOnlyList<string> CandidateGroupIds => _scopeResolver.CandidateGroupIds;

    public bool IsAuthenticated => _scopeResolver.IsAuthenticated;
}
