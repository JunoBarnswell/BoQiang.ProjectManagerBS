using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

/// <summary>
/// Durable marker for one-time Designer migrations and retirement of historical schema.
/// It is deliberately separate from a migration run: runs describe an attempt,
/// while this row describes the last committed schema boundary.
/// </summary>
[SugarTable("app_designer_migration_watermarks")]
public sealed class ApplicationDesignerMigrationWatermarkEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string MigrationKey { get; set; } = string.Empty;
    public string SourceSchemaFingerprint { get; set; } = string.Empty;
    public string TargetSchemaFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    public DateTime AppliedTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
}
