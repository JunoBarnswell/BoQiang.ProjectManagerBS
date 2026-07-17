namespace AsterERP.Contracts.Runtime;

public sealed record RuntimePageSchemaResponse(
    string Id,
    string TenantId,
    string AppCode,
    string PageCode,
    string PageName,
    string PageType,
    string? ModelCode,
    string? PermissionCode,
    int VersionNo,
    string ArtifactJson);
