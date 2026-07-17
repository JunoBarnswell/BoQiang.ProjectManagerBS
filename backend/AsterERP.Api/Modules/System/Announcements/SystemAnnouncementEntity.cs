using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Announcements;

[SugarTable("system_announcements")]
public sealed class SystemAnnouncementEntity : EntityBase
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string AnnouncementType { get; set; } = "General";

    public string Scope { get; set; } = "System";

    public int Priority { get; set; }

    public string Status { get; set; } = "Draft";

    [SugarColumn(ColumnName = "IsPinned")]
    public bool IsTop { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ExpiresAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PublishedBy { get; set; }

    [SugarColumn(ColumnName = "RevokedAt", IsNullable = true)]
    public DateTime? WithdrawnAt { get; set; }
}
