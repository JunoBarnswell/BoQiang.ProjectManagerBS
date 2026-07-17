using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_microflow_revisions")]
public sealed class ApplicationMicroflowRevisionEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string MicroflowId { get; set; } = string.Empty;
    public int RevisionNo { get; set; }
    public string Status { get; set; } = "Draft";
    [SugarColumn(Length = 262144)]
    public string ConfigJson { get; set; } = "{}";
    public string ContentHash { get; set; } = string.Empty;
    public string? ValidationStatus { get; set; }
    [SugarColumn(Length = 2000, IsNullable = true)]
    public string? ValidationMessage { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? PublishedSnapshotId { get; set; }
    public DateTime? PublishedAt { get; set; }
}
