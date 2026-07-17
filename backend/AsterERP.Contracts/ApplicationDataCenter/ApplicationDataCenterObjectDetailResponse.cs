namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterObjectDetailResponse(
    string Id,
    string ModuleKey,
    string ObjectCode,
    string ObjectName,
    string ObjectType,
    string Status,
    int VersionNo,
    string? OwnerUserId,
    string? OwnerName,
    string? Environment,
    string? Endpoint,
    string ConfigJson,
    string? PublicConfigJson,
    string? LastValidationStatus,
    string? LastValidationMessage,
    DateTime? LastValidatedAt,
    ApplicationDataCenterReferenceSummaryResponse ReferenceSummary,
    IReadOnlyList<ApplicationDataCenterNextActionResponse> NextActions,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    string? Remark);
