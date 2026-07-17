using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_runtime_artifacts")]
public sealed class ApplicationDesignerRuntimeArtifactEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string RevisionId { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ArtifactJson { get; set; } = "{}";
    public string ArtifactHash { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string TargetHash { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ManifestJson { get; set; } = "[]";
    public string SignatureHash { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    public string CompilerRevision { get; set; } = string.Empty;
    public string MigrationRevision { get; set; } = string.Empty;
    [SugarColumn(IsNullable = true)]
    public string? SourceArtifactId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? SourceArtifactHash { get; set; }
    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? SourceArtifactJson { get; set; }
    public string Status { get; set; } = "Draft";
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
    [SugarColumn(IsNullable = true)]
    public DateTime? PublishedTime { get; set; }
}
