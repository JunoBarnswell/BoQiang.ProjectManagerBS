using Volo.Abp.Users;

namespace AsterERP.Api.Infrastructure.Security;

public static class AbpCurrentUserAsterErpExtensions
{
    public static string GetAsterErpUserId(this ICurrentUser currentUser) =>
        currentUser.FindClaim(AsterErpClaimTypes.UserId)?.Value ?? string.Empty;

    public static string? GetAsterErpTenantId(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.TenantId)?.Value);

    public static string? GetAsterErpTenantName(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.TenantName)?.Value);

    public static string? GetAsterErpAppCode(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.AppCode)?.Value);

    public static string? GetAsterErpAppName(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.AppName)?.Value);

    public static string? GetAsterErpEmploymentId(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.EmploymentId)?.Value);

    public static string? GetAsterErpEmploymentName(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.EmploymentName)?.Value);

    public static string? GetAsterErpDeptId(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.DeptId)?.Value);

    public static IReadOnlyList<string> GetAsterErpDeptIds(this ICurrentUser currentUser)
    {
        var repeated = GetClaimValues(currentUser, AsterErpClaimTypes.DeptIds);
        if (repeated.Count > 0)
        {
            return repeated;
        }

        var current = currentUser.GetAsterErpDeptId();
        return string.IsNullOrWhiteSpace(current) ? [] : [current];
    }

    public static string? GetAsterErpPositionId(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.PositionId)?.Value);

    public static IReadOnlyList<string> GetAsterErpPositionIds(this ICurrentUser currentUser)
    {
        var repeated = GetClaimValues(currentUser, AsterErpClaimTypes.PositionIds);
        if (repeated.Count > 0)
        {
            return repeated;
        }

        var current = currentUser.GetAsterErpPositionId();
        return string.IsNullOrWhiteSpace(current) ? [] : [current];
    }

    public static IReadOnlyList<string> GetAsterErpRoleIds(this ICurrentUser currentUser) =>
        GetClaimValues(currentUser, AsterErpClaimTypes.RoleId);

    public static IReadOnlyList<string> GetAsterErpRoleCodes(this ICurrentUser currentUser) =>
        GetClaimValues(currentUser, AsterErpClaimTypes.RoleCode);

    public static IReadOnlyList<string> GetAsterErpPermissionCodes(this ICurrentUser currentUser) =>
        GetClaimValues(currentUser, AsterErpClaimTypes.PermissionCode);

    public static string GetAsterErpDataScope(this ICurrentUser currentUser) =>
        Normalize(currentUser.FindClaim(AsterErpClaimTypes.DataScope)?.Value) ?? "SELF";

    public static bool IsAsterErpAuthenticated(this ICurrentUser currentUser) =>
        !string.IsNullOrWhiteSpace(currentUser.GetAsterErpUserId());

    public static bool IsAsterErpPlatformAdmin(this ICurrentUser currentUser) =>
        IsTrue(currentUser.FindClaim(AsterErpClaimTypes.IsPlatformAdmin)?.Value);

    public static bool IsAsterErpTenantAdmin(this ICurrentUser currentUser) =>
        IsTrue(currentUser.FindClaim(AsterErpClaimTypes.IsTenantAdmin)?.Value);

    public static bool HasAsterErpPermission(this ICurrentUser currentUser, string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            return false;
        }

        var permissions = currentUser.GetAsterErpPermissionCodes();
        return permissions.Contains("*", StringComparer.OrdinalIgnoreCase) ||
               permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetClaimValues(ICurrentUser currentUser, string claimType) =>
        currentUser.FindClaims(claimType)
            .Select(claim => Normalize(claim.Value))
            .Where(value => value is not null)
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsTrue(string? value) =>
        bool.TryParse(value, out var result) && result;
}
