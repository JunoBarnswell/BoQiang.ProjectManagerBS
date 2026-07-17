namespace AsterERP.Contracts.System.Announcements;

public sealed record AnnouncementListItemResponse(
    string Id,
    string Title,
    string Content,
    string AnnouncementType,
    string Scope,
    int Priority,
    string Status,
    string EffectiveStatus,
    bool IsPinned,
    DateTime? ExpiresAt,
    DateTime? PublishedAt,
    string? PublishedBy,
    DateTime? RevokedAt,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    string? Remark);
