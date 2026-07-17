using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_migration_runs")]
public sealed class ApplicationDesignerMigrationRunEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string MigrationKey { get; set; } = string.Empty;
    public string MaintenanceLockId { get; set; } = string.Empty;
    public string Status { get; set; } = "Running";
    [SugarColumn(ColumnDataType = "TEXT")]
    public string BackupPayloadJson { get; set; } = "{}";
    public string BackupSha256 { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? SourceCommit { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? TargetCommit { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? PreviousArtifactId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? PublishedArtifactId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? HealthCheckId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? RollbackPointer { get; set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
    public DateTime StartedTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedTime { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? LockExpiresTime { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? OperatorUserId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }
}
