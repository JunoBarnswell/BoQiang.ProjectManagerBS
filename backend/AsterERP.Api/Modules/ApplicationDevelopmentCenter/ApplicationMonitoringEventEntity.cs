using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_application_monitoring_events")]
public sealed class ApplicationMonitoringEventEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? PageId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? RevisionId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? ArtifactHash { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public bool Success { get; set; }
    [SugarColumn(IsNullable = true)]
    public long? DurationMs { get; set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string PayloadJson { get; set; } = "{}";
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
