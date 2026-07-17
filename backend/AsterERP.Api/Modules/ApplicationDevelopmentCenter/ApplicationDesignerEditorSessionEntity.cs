using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_editor_sessions")]
public sealed class ApplicationDesignerEditorSessionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SessionKey { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string SelectedNodeIdsJson { get; set; } = "[]";
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ViewportJson { get; set; } = "{}";
    [SugarColumn(IsNullable = true)]
    public string? ActiveRevisionId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime LastSeenTime { get; set; } = DateTime.UtcNow;
}
