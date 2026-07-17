namespace AsterERP.Api.Application.Auth;

public sealed class WorkspaceQueryRow
{
    public string TenantId { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public string TenantStatus { get; set; } = string.Empty;

    public DateTime? TenantExpiredAt { get; set; }

    public string AppCode { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string AppStatus { get; set; } = string.Empty;

    public string? AppRemark { get; set; }

    public string? AppDefaultRoutePath { get; set; }

    public string? AppAdminDefaultRoutePath { get; set; }

    public string? LogoFileId { get; set; }

    public string TenantAppStatus { get; set; } = string.Empty;

    public string? TenantAppSystemName { get; set; }

    public DateTime? TenantAppExpiredAt { get; set; }

    public string? TenantAppRemark { get; set; }

    public string? TenantAppConfigJson { get; set; }

    public string MembershipStatus { get; set; } = string.Empty;

    public bool MembershipIsDefault { get; set; }

    public bool UserRoleIsDefault { get; set; }
}
