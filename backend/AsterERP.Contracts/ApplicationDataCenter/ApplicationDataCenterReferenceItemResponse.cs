namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterReferenceItemResponse(
    string Id,
    string SourceModule,
    string SourceObjectId,
    string SourceObjectCode,
    string SourceObjectName,
    string TargetModule,
    string TargetObjectId,
    string ReferenceKind,
    string Status,
    string? OwnerUserId,
    DateTime CreatedTime);
