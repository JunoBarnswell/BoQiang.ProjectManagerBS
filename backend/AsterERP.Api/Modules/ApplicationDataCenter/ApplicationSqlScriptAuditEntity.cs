using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_sql_script_audits")]
public sealed class ApplicationSqlScriptAuditEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public string SourceKind { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? SourceName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DataSourceId { get; set; }

    public string ScriptHash { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ScriptPreview { get; set; } = string.Empty;

    public string StatementSummary { get; set; } = string.Empty;

    public string RiskSummary { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string Operation { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string ResourceKind { get; set; } = string.Empty;

    [SugarColumn(Length = 255, IsNullable = true)]
    public string? PermissionCode { get; set; }

    [SugarColumn(Length = 64)]
    public string Outcome { get; set; } = "Pending";

    [SugarColumn(Length = 128, IsNullable = true)]
    public string? FailureCode { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? Provider { get; set; }

    public int TimeoutMs { get; set; }

    public bool CancellationRequested { get; set; }

    [SugarColumn(Length = 128)]
    public string ActorUserId { get; set; } = string.Empty;

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(Length = 128)]
    public string RequestHash { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT")]
    public string RedactedDetailsJson { get; set; } = "{}";

    public string ParameterSummaryJson { get; set; } = "[]";

    public int AffectedRows { get; set; }

    public int ReturnedRows { get; set; }

    public long DurationMs { get; set; }

    public bool IsSuccess { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
