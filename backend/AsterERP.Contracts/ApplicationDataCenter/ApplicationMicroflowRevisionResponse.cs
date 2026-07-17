namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationMicroflowRevisionResponse(
    string Id,
    int RevisionNo,
    string Status,
    string ConfigJson,
    string? ValidationStatus,
    string? ValidationMessage,
    DateTime? ValidatedAt,
    DateTime CreatedTime,
    DateTime? PublishedAt,
    bool IsCurrent);
