namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentSharedResourceUpsertRequest
{
    public string ContentJson { get; set; } = "{}";

    public string? ContentText { get; set; }

    public string ResourceCode { get; set; } = string.Empty;

    public string ResourceName { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string? VersionId { get; set; }
}
