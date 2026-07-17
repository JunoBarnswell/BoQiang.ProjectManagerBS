namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPublishResponse
{
    public IReadOnlyList<ApplicationDevelopmentPublishDiagnosticDto> Diagnostics { get; set; } = [];

    public IReadOnlyList<string> GeneratedPermissionCodes { get; set; } = [];

    public string? PublishedMenuCode { get; set; }

    public string? PublishedMenuId { get; set; }

    public string? PublishedArtifactId { get; set; }

    public string? PublishedArtifactHash { get; set; }

    public int? PublishedArtifactRevision { get; set; }

    public string? PublishedManifestHash { get; set; }

    public string? PublishedRoutePath { get; set; }

    public DateTime? PublishedSchemaUpdatedTime { get; set; }

    public string VersionId { get; set; } = string.Empty;
}
