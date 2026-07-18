namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementSavedViewUpsertRequest(
    string ViewName,
    string ViewKey,
    string QueryJson,
    bool IsShared = false,
    bool IsDefault = false,
    long VersionNo = 0);

public sealed record ProjectManagementSavedViewResponse(
    string Id,
    string ProjectId,
    string ViewName,
    string ViewKey,
    string QueryJson,
    string OwnerUserId,
    bool IsShared,
    bool IsDefault,
    long VersionNo,
    DateTime CreatedTime,
    DateTime? UpdatedTime);
