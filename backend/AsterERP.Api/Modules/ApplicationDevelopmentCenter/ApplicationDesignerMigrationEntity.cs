using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_migrations")]
public sealed class ApplicationDesignerMigrationEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string TargetHash { get; set; } = string.Empty;
    public string MigrationRevision { get; set; } = string.Empty;
    public string Status { get; set; } = "Completed";
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
    [SugarColumn(ColumnDataType = "TEXT")]
    public string BackupDocumentJson { get; set; } = "{}";
    [SugarColumn(IsNullable = true)]
    public string? BackupLocation { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? RollbackRevisionId { get; set; }
    public DateTime StartedTime { get; set; } = DateTime.UtcNow;
    [SugarColumn(IsNullable = true)]
    public DateTime? CompletedTime { get; set; }
}
