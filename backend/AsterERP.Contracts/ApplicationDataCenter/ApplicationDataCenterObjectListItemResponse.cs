namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterObjectListItemResponse(
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
    string? LastValidationStatus,
    string? LastValidationMessage,
    DateTime? LastValidatedAt,
    int ReferenceCount,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    string? Remark);
