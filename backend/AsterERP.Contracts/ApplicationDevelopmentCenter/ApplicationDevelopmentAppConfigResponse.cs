namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentAppConfigResponse
{
    public string? DefaultDataSourceId { get; set; }

    public string? Description { get; set; }

    public string? LogoIcon { get; set; }

    public string? PrimaryColor { get; set; }

    public bool SqlProtectionEnabled { get; set; }

    public string? SystemFullName { get; set; }

    public string? SystemShortName { get; set; }
}
