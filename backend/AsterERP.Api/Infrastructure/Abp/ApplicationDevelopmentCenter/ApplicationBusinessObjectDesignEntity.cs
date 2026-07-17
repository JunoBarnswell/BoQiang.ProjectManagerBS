using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Abp.ApplicationDevelopmentCenter;

[SugarTable("app_business_object_designs")]
public sealed class ApplicationBusinessObjectDesignEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string VersionId { get; set; } = string.Empty;

    public string PageId { get; set; } = string.Empty;

    public string PageCode { get; set; } = string.Empty;

    public string PageName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ModuleId { get; set; }

    public string ModelCode { get; set; } = string.Empty;

    public string ModelName { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? DataSourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SourceTable { get; set; }

    public string ProviderKey { get; set; } = "application-data-center.sql-table";

    [SugarColumn(IsNullable = true)]
    public string? KeyField { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string FieldsJson { get; set; } = "[]";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string PermissionConfigJson { get; set; } = "{}";

    public bool CreateWorkflowBinding { get; set; }

    public string Status { get; set; } = "Draft";
}
