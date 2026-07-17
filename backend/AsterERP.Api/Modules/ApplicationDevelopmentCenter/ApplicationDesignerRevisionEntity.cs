using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_revisions")]
public sealed class ApplicationDesignerRevisionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public int RevisionNumber { get; set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DocumentJson { get; set; } = "{}";
    public string DocumentHash { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string TargetHash { get; set; } = string.Empty;
    public string MigrationRevision { get; set; } = string.Empty;
    public string CompilerRevision { get; set; } = string.Empty;
    public string ManifestHash { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ManifestJson { get; set; } = "{}";
    [SugarColumn(IsNullable = true)]
    public string? SourceArtifactHash { get; set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string ChangeSetJson { get; set; } = "{}";
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
}
