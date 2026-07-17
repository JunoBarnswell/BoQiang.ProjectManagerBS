namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationDataObjectIdentity(
    string ModuleKey,
    string ObjectId,
    string ObjectCode,
    string ObjectName,
    string? OwnerUserId);
