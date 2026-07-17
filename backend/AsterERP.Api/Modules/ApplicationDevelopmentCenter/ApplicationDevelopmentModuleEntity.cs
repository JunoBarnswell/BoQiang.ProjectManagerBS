using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDevelopmentCenter;

[SugarTable("app_dev_modules")]
public sealed class ApplicationDevelopmentModuleEntity : EntityBase
{
    public string AppCode { get; set; } = string.Empty;

    public string ModuleCode { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ParentModuleId { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = "Draft";

    public string TenantId { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;
}
