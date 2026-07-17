using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_object_references")]
public sealed class ApplicationDataObjectReferenceEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string SourceModule { get; set; } = string.Empty;

    public string SourceObjectId { get; set; } = string.Empty;

    public string SourceObjectCode { get; set; } = string.Empty;

    public string SourceObjectName { get; set; } = string.Empty;

    public string TargetModule { get; set; } = string.Empty;

    public string TargetObjectId { get; set; } = string.Empty;

    public string ReferenceKind { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    [SugarColumn(IsNullable = true)]
    public string? OwnerUserId { get; set; }
}
