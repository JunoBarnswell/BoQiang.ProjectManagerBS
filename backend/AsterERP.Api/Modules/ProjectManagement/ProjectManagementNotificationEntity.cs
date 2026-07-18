using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

[SugarTable("pm_notifications")]
public sealed class ProjectManagementNotificationEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetRoute { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)] public string? ProjectId { get; set; }
    [SugarColumn(IsNullable = true)] public string? TaskId { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    [SugarColumn(IsNullable = true)] public DateTime? ReadTime { get; set; }
}
