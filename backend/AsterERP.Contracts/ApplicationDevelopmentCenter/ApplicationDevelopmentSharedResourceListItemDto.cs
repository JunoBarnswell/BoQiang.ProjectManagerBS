namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentSharedResourceListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string ResourceCode { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }

    public string? VersionId { get; set; }
}
