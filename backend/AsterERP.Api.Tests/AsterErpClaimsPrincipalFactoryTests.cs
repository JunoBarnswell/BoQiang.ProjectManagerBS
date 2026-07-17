using AsterERP.Api.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Volo.Abp.Security.Claims;
using AbpCurrentUser = Volo.Abp.Users.CurrentUser;
using AbpHttpContextCurrentPrincipalAccessor = Volo.Abp.AspNetCore.Security.Claims.HttpContextCurrentPrincipalAccessor;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class AsterErpClaimsPrincipalFactoryTests
{
    [Fact]
    public void Authenticated_user_is_projected_to_abp_and_astererp_claims()
    {
        var userId = Guid.NewGuid().ToString("N");
        var user = new ResolvedAuthenticatedUser(
            userId,
            "admin",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "root",
            "system-admin",
            ["role-id-admin", "role-id-admin", "role-id-finance"],
            ["admin", "admin", "finance"],
            ["system:user:query", "*"],
            "ALL",
            true,
            true,
            true,
            "平台管理员");

        var principal = AsterErpClaimsPrincipalFactory.Create(user);

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal(AsterErpClaimsPrincipalFactory.AuthenticationType, principal.Identity?.AuthenticationType);
        Assert.Equal(userId, principal.FindFirst(AbpClaimTypes.UserId)?.Value);
        Assert.Equal("admin", principal.FindFirst(AbpClaimTypes.UserName)?.Value);
        Assert.Equal("平台管理员", principal.FindFirst(AbpClaimTypes.Name)?.Value);
        Assert.Equal("tenant-system", principal.FindFirst(AbpClaimTypes.TenantId)?.Value);
        Assert.Equal("tenant-system", principal.FindFirst(AsterErpClaimTypes.TenantId)?.Value);
        Assert.Equal("root", principal.FindFirst(AsterErpClaimTypes.DeptId)?.Value);
        Assert.Equal("SYSTEM", principal.FindFirst(AsterErpClaimTypes.AppCode)?.Value);
        Assert.Equal("ALL", principal.FindFirst(AsterErpClaimTypes.DataScope)?.Value);
        Assert.Equal("true", principal.FindFirst(AsterErpClaimTypes.IsPlatformAdmin)?.Value);
        Assert.Equal(["admin", "finance"], principal.FindAll(AbpClaimTypes.Role).Select(claim => claim.Value).ToArray());
        Assert.Equal(["role-id-admin", "role-id-finance"], principal.FindAll(AsterErpClaimTypes.RoleId).Select(claim => claim.Value).ToArray());
        Assert.Equal(["admin", "finance"], principal.FindAll(AsterErpClaimTypes.RoleCode).Select(claim => claim.Value).ToArray());
        Assert.Equal(["system:user:query", "*"], principal.FindAll(AsterErpClaimTypes.PermissionCode).Select(claim => claim.Value).ToArray());

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        var accessor = new AbpHttpContextCurrentPrincipalAccessor(httpContextAccessor);
        var abpCurrentUser = new AbpCurrentUser(accessor);

        Assert.True(abpCurrentUser.IsAuthenticated);
        Assert.True(abpCurrentUser.IsAsterErpAuthenticated());
        Assert.Equal(Guid.ParseExact(userId, "N"), abpCurrentUser.Id);
        Assert.Equal("admin", abpCurrentUser.UserName);
        Assert.Equal("平台管理员", abpCurrentUser.Name);
        Assert.Equal(["admin", "finance"], abpCurrentUser.Roles);
        Assert.Equal("tenant-system", abpCurrentUser.FindClaim(AsterErpClaimTypes.TenantId)?.Value);
        Assert.Equal("root", abpCurrentUser.FindClaim(AsterErpClaimTypes.DeptId)?.Value);
        Assert.Equal(userId, abpCurrentUser.GetAsterErpUserId());
        Assert.Equal("tenant-system", abpCurrentUser.GetAsterErpTenantId());
        Assert.Equal("默认租户", abpCurrentUser.GetAsterErpTenantName());
        Assert.Equal("SYSTEM", abpCurrentUser.GetAsterErpAppCode());
        Assert.Equal("系统管理", abpCurrentUser.GetAsterErpAppName());
        Assert.Equal("root", abpCurrentUser.GetAsterErpDeptId());
        Assert.Equal("system-admin", abpCurrentUser.GetAsterErpPositionId());
        Assert.Equal(["role-id-admin", "role-id-finance"], abpCurrentUser.GetAsterErpRoleIds());
        Assert.Equal(["admin", "finance"], abpCurrentUser.GetAsterErpRoleCodes());
        Assert.Equal(["system:user:query", "*"], abpCurrentUser.GetAsterErpPermissionCodes());
        Assert.Equal("ALL", abpCurrentUser.GetAsterErpDataScope());
        Assert.True(abpCurrentUser.IsAsterErpPlatformAdmin());
        Assert.True(abpCurrentUser.IsAsterErpTenantAdmin());
        Assert.True(abpCurrentUser.HasAsterErpPermission("SYSTEM:USER:QUERY"));
        Assert.True(abpCurrentUser.HasAsterErpPermission("unknown:permission"));
    }

    [Fact]
    public void Astererp_extensions_keep_string_business_ids_without_requiring_abp_guid_semantics()
    {
        var user = new ResolvedAuthenticatedUser(
            "admin",
            "admin",
            "tenant-system",
            "默认租户",
            "SYSTEM",
            "系统管理",
            "root",
            "system-admin",
            ["role-id-admin"],
            ["admin"],
            ["system:menu:query"],
            "ALL",
            true,
            true,
            true,
            "平台管理员");

        var principal = AsterErpClaimsPrincipalFactory.Create(user);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        var accessor = new AbpHttpContextCurrentPrincipalAccessor(httpContextAccessor);
        var abpCurrentUser = new AbpCurrentUser(accessor);

        Assert.True(abpCurrentUser.IsAsterErpAuthenticated());
        Assert.Equal("admin", abpCurrentUser.GetAsterErpUserId());
        Assert.Equal("tenant-system", abpCurrentUser.GetAsterErpTenantId());
        Assert.Equal("SYSTEM", abpCurrentUser.GetAsterErpAppCode());
        Assert.True(abpCurrentUser.HasAsterErpPermission("SYSTEM:MENU:QUERY"));
    }

    [Fact]
    public void Anonymous_user_is_not_projected_as_authenticated_abp_user()
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

        var principal = AsterErpClaimsPrincipalFactory.Create(user);

        Assert.False(principal.Identity?.IsAuthenticated);
        Assert.Empty(principal.Claims);

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal
            }
        };
        var accessor = new AbpHttpContextCurrentPrincipalAccessor(httpContextAccessor);
        var abpCurrentUser = new AbpCurrentUser(accessor);

        Assert.False(abpCurrentUser.IsAuthenticated);
        Assert.False(abpCurrentUser.IsAsterErpAuthenticated());
        Assert.Equal(string.Empty, abpCurrentUser.GetAsterErpUserId());
        Assert.Null(abpCurrentUser.GetAsterErpTenantId());
        Assert.Null(abpCurrentUser.GetAsterErpAppCode());
        Assert.Null(abpCurrentUser.GetAsterErpDeptId());
        Assert.Empty(abpCurrentUser.GetAsterErpRoleIds());
        Assert.Empty(abpCurrentUser.GetAsterErpPermissionCodes());
        Assert.Equal("SELF", abpCurrentUser.GetAsterErpDataScope());
        Assert.False(abpCurrentUser.IsAsterErpPlatformAdmin());
        Assert.False(abpCurrentUser.IsAsterErpTenantAdmin());
        Assert.False(abpCurrentUser.HasAsterErpPermission("system:user:query"));
    }
}
