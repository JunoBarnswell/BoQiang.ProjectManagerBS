namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentSharedResourceDetailDto
{
    public string ContentJson { get; set; } = "{}";

    public string? ContentText { get; set; }

    public string Id { get; set; } = string.Empty;

    public string ResourceCode { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? VersionId { get; set; }
}
