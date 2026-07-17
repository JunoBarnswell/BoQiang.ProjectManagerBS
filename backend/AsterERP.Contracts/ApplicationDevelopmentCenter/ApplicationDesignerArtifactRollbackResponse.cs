namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDesignerArtifactRollbackResponse
{
    public string AuditId { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string PageId { get; set; } = string.Empty;

    public string ArtifactId { get; set; } = string.Empty;

    public string ArtifactHash { get; set; } = string.Empty;

    public string PreviousArtifactId { get; set; } = string.Empty;

    public string PublishedArtifactId { get; set; } = string.Empty;

    public string Status { get; set; } = "Succeeded";
}
