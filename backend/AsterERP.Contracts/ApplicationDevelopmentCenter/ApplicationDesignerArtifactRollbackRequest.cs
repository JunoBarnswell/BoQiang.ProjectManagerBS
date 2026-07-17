namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDesignerArtifactRollbackRequest
{
    public string ArtifactId { get; set; } = string.Empty;

    public string ArtifactHash { get; set; } = string.Empty;

    public string OperationId { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;
}
