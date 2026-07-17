namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentVersionDto
{
    public string Id { get; set; } = string.Empty;

    public string? DefaultPageId { get; set; }

    public string? SourceDataSourceId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string VersionCode { get; set; } = string.Empty;

    public string VersionName { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}
