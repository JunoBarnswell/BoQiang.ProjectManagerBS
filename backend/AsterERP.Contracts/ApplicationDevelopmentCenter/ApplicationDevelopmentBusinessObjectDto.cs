namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentBusinessObjectDto
{
    public string Id { get; set; } = string.Empty;

    public string PageId { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;

    public string? ModuleId { get; set; }

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string? DataSourceId { get; set; }

    public string? SourceTable { get; set; }

    public string ProviderKey { get; set; } = string.Empty;

    public string? KeyField { get; set; }

    public bool ReadOnly { get; set; }

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public IReadOnlyList<ApplicationDevelopmentBusinessObjectFieldDto> Fields { get; set; } = [];

    public string Status { get; set; } = "Draft";

    public DateTime? UpdatedTime { get; set; }
}
