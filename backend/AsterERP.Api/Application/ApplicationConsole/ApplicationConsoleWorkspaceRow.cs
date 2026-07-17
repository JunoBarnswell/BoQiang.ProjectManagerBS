namespace AsterERP.Api.Application.ApplicationConsole;

public sealed class ApplicationConsoleWorkspaceRow
{
    public string TenantAppId { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string AppName { get; set; } = string.Empty;

    public string? SystemName { get; set; }

    public string? Version { get; set; }

    public string? DefaultRoutePath { get; set; }

    public string Status { get; set; } = "Enabled";

    public string AppType { get; set; } = "Business";

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }

    public string? TenantAppConfigJson { get; set; }
}
