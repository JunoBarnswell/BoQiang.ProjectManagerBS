using System.Security.Claims;
using Volo.Abp.Security.Claims;

namespace AsterERP.Api.Infrastructure.Security;

public static class AsterErpClaimsPrincipalFactory
{
    public const string AuthenticationType = "AsterERP";

    public static ClaimsPrincipal Create(ResolvedAuthenticatedUser user)
    {
        if (!user.IsAuthenticated)
        {
            return new ClaimsPrincipal(new ClaimsIdentity());
        }

        var claims = new List<Claim>
        {
            CreateClaim(AbpClaimTypes.UserId, user.UserId),
            CreateClaim(AbpClaimTypes.UserName, user.UserName),
            CreateClaim(AbpClaimTypes.Name, ResolveDisplayName(user)),
            CreateClaim(AsterErpClaimTypes.UserId, user.UserId),
            CreateClaim(AsterErpClaimTypes.DataScope, user.DataScope),
            CreateBooleanClaim(AsterErpClaimTypes.IsPlatformAdmin, user.IsPlatformAdmin),
            CreateBooleanClaim(AsterErpClaimTypes.IsTenantAdmin, user.IsTenantAdmin)
        };

        AddOptionalClaim(claims, AbpClaimTypes.TenantId, user.TenantId);
        AddOptionalClaim(claims, AsterErpClaimTypes.TenantId, user.TenantId);
        AddOptionalClaim(claims, AsterErpClaimTypes.TenantName, user.TenantName);
        AddOptionalClaim(claims, AsterErpClaimTypes.AppCode, user.AppCode);
        AddOptionalClaim(claims, AsterErpClaimTypes.AppName, user.AppName);
        AddOptionalClaim(claims, AsterErpClaimTypes.EmploymentId, user.EmploymentId);
        AddOptionalClaim(claims, AsterErpClaimTypes.EmploymentName, user.EmploymentName);
        AddOptionalClaim(claims, AsterErpClaimTypes.DeptId, user.DeptId);
        AddOptionalClaim(claims, AsterErpClaimTypes.PositionId, user.PositionId);

        AddRepeatedClaims(claims, AbpClaimTypes.Role, ResolveAbpRoles(user));
        AddRepeatedClaims(claims, AsterErpClaimTypes.DeptIds, user.DeptIds ?? []);
        AddRepeatedClaims(claims, AsterErpClaimTypes.PositionIds, user.PositionIds ?? []);
        AddRepeatedClaims(claims, AsterErpClaimTypes.RoleId, user.RoleIds);
        AddRepeatedClaims(claims, AsterErpClaimTypes.RoleCode, user.RoleCodes);
        AddRepeatedClaims(claims, AsterErpClaimTypes.PermissionCode, user.PermissionCodes);

        return new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            AuthenticationType,
            AbpClaimTypes.UserName,
            AbpClaimTypes.Role));
    }

    private static string ResolveDisplayName(ResolvedAuthenticatedUser user) =>
        string.IsNullOrWhiteSpace(user.DisplayName)
            ? user.UserName
            : user.DisplayName;

    private static IEnumerable<string> ResolveAbpRoles(ResolvedAuthenticatedUser user) =>
        user.RoleCodes.Count == 0 ? user.RoleIds : user.RoleCodes;

    private static void AddOptionalClaim(List<Claim> claims, string type, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            claims.Add(CreateClaim(type, value));
        }
    }

    private static void AddRepeatedClaims(List<Claim> claims, string type, IEnumerable<string> values)
    {
        foreach (var value in values.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            claims.Add(CreateClaim(type, value));
        }
    }

    private static Claim CreateClaim(string type, string value) => new(type, value);

    private static Claim CreateBooleanClaim(string type, bool value) =>
        new(type, value ? "true" : "false", ClaimValueTypes.Boolean);
}
