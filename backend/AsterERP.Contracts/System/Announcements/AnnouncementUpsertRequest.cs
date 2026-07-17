namespace AsterERP.Contracts.System.Announcements;

public sealed class AnnouncementUpsertRequest
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string AnnouncementType { get; set; } = "General";

    public string Scope { get; set; } = "System";

    public int Priority { get; set; }

    public bool IsPinned { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public string? Remark { get; set; }
}
