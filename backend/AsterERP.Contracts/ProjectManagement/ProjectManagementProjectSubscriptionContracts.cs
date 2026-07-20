namespace AsterERP.Contracts.ProjectManagement;

public sealed record ProjectManagementProjectSubscriptionResponse(
    string ProjectId,
    string UserId,
    string Mode,
    long VersionNo,
    DateTime? UpdatedTime);

public sealed record ProjectManagementProjectSubscriptionUpsertRequest(
    string Mode,
    long? VersionNo = null);
