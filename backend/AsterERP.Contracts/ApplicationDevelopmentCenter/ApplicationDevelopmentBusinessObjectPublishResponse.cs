namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed class ApplicationDevelopmentBusinessObjectPublishResponse
{
    public string BusinessObjectId { get; set; } = string.Empty;

    public string PageCode { get; set; } = string.Empty;

    public string ModelCode { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string ArtifactId { get; set; } = string.Empty;

    public string DataModelId { get; set; } = string.Empty;

    public IReadOnlyList<string> GeneratedPermissionCodes { get; set; } = [];

    public IReadOnlyList<string> Warnings { get; set; } = [];

    public IReadOnlyList<ApplicationDevelopmentNextActionDto> NextActions { get; set; } = [];
}
