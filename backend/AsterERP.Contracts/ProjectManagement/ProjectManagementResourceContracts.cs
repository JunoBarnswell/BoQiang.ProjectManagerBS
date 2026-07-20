namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementResourceUpsertRequest(
    string ResourceName,
    string ResourceUrl,
    string? Description = null,
    long VersionNo = 0);

public sealed record ProjectManagementResourceResponse(
    string Id,
    string ProjectId,
    string ResourceName,
    string ResourceUrl,
    string? Description,
    long VersionNo,
    DateTime CreatedTime,
    DateTime? UpdatedTime);
