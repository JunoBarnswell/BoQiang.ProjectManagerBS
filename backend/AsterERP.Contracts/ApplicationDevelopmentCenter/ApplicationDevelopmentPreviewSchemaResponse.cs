namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentPreviewSchemaResponse
{
    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    public string ArtifactJson { get; set; } = "{}";
}
