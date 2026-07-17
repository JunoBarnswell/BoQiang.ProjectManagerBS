namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentVersionUpsertRequest
{
    public string? DefaultPageId { get; set; }

    public string? Remark { get; set; }

    public string? SourceDataSourceId { get; set; }

    public string Status { get; set; } = "Draft";

    public string VersionCode { get; set; } = string.Empty;

    public string VersionName { get; set; } = string.Empty;
}
