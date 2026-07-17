using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ApplicationDataCenter;

[SugarTable("app_data_mutation_ledgers")]
public sealed class ApplicationDataMutationLedgerEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ActorUserId { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string RequestHash { get; set; } = string.Empty;

    public string Status { get; set; } = "Executing";

    public string ResourceKind { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ResourceId { get; set; }

    public string DataSourceId { get; set; } = string.Empty;

    public string ObjectName { get; set; } = string.Empty;

    public string StatementSummary { get; set; } = string.Empty;

    [SugarColumn(Length = 128)]
    public string StatementHash { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public int? ExpectedAffectedRows { get; set; }

    public int AffectedRows { get; set; }

    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? ExecutingAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LeaseExpiresAt { get; set; }

    [SugarColumn(Length = 64, IsNullable = true)]
    public string? LeaseToken { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? FinalizedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FailureCode { get; set; }

    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(Length = 4000, IsNullable = true)]
    public string? StatusReason { get; set; }

    [SugarColumn(ColumnDataType = "TEXT", IsNullable = true)]
    public string? StatusHistoryJson { get; set; }

    [SugarColumn(Length = 8000, IsNullable = true)]
    public string? ReconcileEvidenceJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ReconciledBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? ReconciledAt { get; set; }
}
