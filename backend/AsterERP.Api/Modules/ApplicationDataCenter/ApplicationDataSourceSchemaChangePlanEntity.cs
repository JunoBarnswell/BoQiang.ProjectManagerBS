using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_source_schema_change_plans")]
public sealed class ApplicationDataSourceSchemaChangePlanEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;

    [SugarColumn(Length = 1048576)]
    public string SqlPreview { get; set; } = string.Empty;

    [SugarColumn(Length = 1048576)]
    public string RisksJson { get; set; } = "[]";

    public string RiskLevel { get; set; } = "low";
    public bool RequiresLock { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? EstimatedAffectedRows { get; set; }

    [SugarColumn(IsNullable = true, Length = 32)]
    public string? EstimatedAffectedRowsStatus { get; set; }

    [SugarColumn(Length = 1048576)]
    public string DependenciesJson { get; set; } = "[]";

    [SugarColumn(Length = 4000)]
    public string BeforeColumnsJson { get; set; } = "[]";

    [SugarColumn(Length = 4000)]
    public string AfterColumnsJson { get; set; } = "[]";

    public bool RequiresConfirmation { get; set; }
    public bool Reversible { get; set; }
    public string PlanHash { get; set; } = string.Empty;
    public DateTime PlannedAt { get; set; }
    public string Status { get; set; } = "Planned";
}
