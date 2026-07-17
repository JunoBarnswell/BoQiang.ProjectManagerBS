using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_publish_records")]
public sealed class ApplicationDesignerPublishRecordEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? PageId { get; set; }
    public string RevisionId { get; set; } = string.Empty;
    public string ArtifactId { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public string DocumentHash { get; set; } = string.Empty;
    public string ArtifactHash { get; set; } = string.Empty;
    public string CompilerRevision { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ManifestJson { get; set; } = "{}";
    public string MigrationRevision { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? SourceArtifactId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? SourceArtifactHash { get; set; }
    public string SourceHash { get; set; } = string.Empty;
    public string TargetHash { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string OperationType { get; set; } = "Publish";
    [SugarColumn(IsNullable = true)]
    public string? OperationId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? TargetArtifactId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? TargetArtifactHash { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? OperatorUserId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? RollbackReason { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? FailureCode { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? FailureMessage { get; set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
    [SugarColumn(IsNullable = true)]
    public string? BackupLocation { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? RollbackRevisionId { get; set; }
    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedTime { get; set; }
}
