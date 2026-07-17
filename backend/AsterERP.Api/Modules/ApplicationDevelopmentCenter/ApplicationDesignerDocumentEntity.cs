using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_designer_documents")]
public sealed class ApplicationDesignerDocumentEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string PageId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DocumentJson { get; set; } = "{}";
    public string DocumentHash { get; set; } = string.Empty;
    public string SourceHash { get; set; } = string.Empty;
    public string TargetHash { get; set; } = string.Empty;
    public string MigrationRevision { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    [SugarColumn(IsNullable = true)]
    public string? CurrentRevisionId { get; set; }
    [SugarColumn(IsNullable = true)]
    public string? PublishedArtifactId { get; set; }
    [SugarColumn(ColumnDataType = "TEXT")]
    public string DiagnosticsJson { get; set; } = "[]";
}
