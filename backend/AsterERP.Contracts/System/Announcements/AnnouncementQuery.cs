namespace AsterERP.Contracts.System.Announcements;

public sealed class AnnouncementQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? Status { get; set; }

    public string? AnnouncementType { get; set; }
}
