using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Volo.Abp.AspNetCore.Security.Claims;
using Volo.Abp.Security.Claims;
using Volo.Abp.Users;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowCurrentUserContextTests
{
    [Fact]
    public void Project_authenticated_user_claims_into_workflow_context()
    {
        var user = new ResolvedAuthenticatedUser(
            "user-001",
            "alice",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "dept-001",
            "pos-001",
            ["role-id-admin", "role-id-finance"],
            ["admin", "finance"],
            ["workflow:task:query"],
            "ALL",
            true,
            true,
            true,
            "Alice");

        var context = CreateContext(user);

        Assert.Equal("user-001", context.UserId);
        Assert.Equal("alice", context.UserName);
        Assert.Equal("tenant-system", context.TenantId);
        Assert.Equal("SYSTEM", context.AppCode);
        Assert.Equal("dept-001", context.DeptId);
        Assert.Equal("pos-001", context.PositionId);
        Assert.Equal(["role-id-admin", "role-id-finance"], context.RoleIds);
        Assert.Equal(
            [
                "role:role-id-admin",
                "role:role-id-finance",
                "dept:dept-001",
                "position:pos-001"
            ],
            context.CandidateGroupIds);
        Assert.True(context.IsAuthenticated);
    }

    [Fact]
    public void Anonymous_user_has_empty_business_identity_and_no_candidate_groups()
    {
        var user = new ResolvedAuthenticatedUser(
            "anonymous",
            "anonymous",
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            [],
            [],
            "SELF",
            false,
            false,
            false);

        var context = CreateContext(user);

        Assert.Equal(string.Empty, context.UserId);
        Assert.Equal(string.Empty, context.UserName);
        Assert.Null(context.TenantId);
        Assert.Null(context.AppCode);
        Assert.Null(context.DeptId);
        Assert.Null(context.PositionId);
        Assert.Empty(context.RoleIds);
        Assert.Empty(context.CandidateGroupIds);
        Assert.False(context.IsAuthenticated);
    }

    private static ICurrentUser CreateCurrentUser(ResolvedAuthenticatedUser user)
    {
        var principal = AsterErpClaimsPrincipalFactory.Create(user);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        return new CurrentUser(new HttpContextCurrentPrincipalAccessor(httpContextAccessor));
    }

    private static WorkflowCurrentUserContext CreateContext(ResolvedAuthenticatedUser user)
    {
        return new WorkflowCurrentUserContext(new WorkflowIdentityScopeResolver(CreateCurrentUser(user)));
    }

    [Fact]
    public void Workflow_identity_candidate_scope_parses_user_and_group_scopes()
    {
        var user = new ResolvedAuthenticatedUser(
            "user-007",
            "ivy",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "dept-7",
            "pos-9",
            ["role-a", "role-a", "role-b"],
            ["admin", "finance"],
            ["workflow:task:query"],
            "ALL",
            true,
            true,
            true,
            "Ivy");

        var currentUser = CreateCurrentUser(user);
        var resolver = new WorkflowIdentityScopeResolver(currentUser);
        var scope = new WorkflowIdentityCandidateScope(resolver);

        Assert.Equal("user-007", scope.CurrentUserId);
        Assert.Equal(["user-007"], scope.CandidateUserIds);
        Assert.Equal(["role-a", "role-b"], scope.CandidateRoleIds);
        Assert.Equal(["role:role-a", "role:role-b"], scope.CandidateRoleGroupIds);
        Assert.Equal(["dept:dept-7"], scope.CandidateDepartmentGroupIds);
        Assert.Equal(["position:pos-9"], scope.CandidatePositionGroupIds);
        Assert.Equal(["role:role-a", "role:role-b", "dept:dept-7", "position:pos-9"], scope.CandidateGroupIds);
        Assert.True(scope.IsCandidateUser("user-007"));
        Assert.False(scope.IsCandidateUser("user-008"));
        Assert.True(scope.IsCandidateGroup("role:role-a"));
        Assert.False(scope.IsCandidateGroup("role:role-missing"));
    }
}
