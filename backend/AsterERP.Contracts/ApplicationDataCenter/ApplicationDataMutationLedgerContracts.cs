namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataMutationLedgerQueryRequest
{
    public string? LedgerId { get; set; }

    public string? RequestHash { get; set; }

    public string? Operation { get; set; }

    public string? Status { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class ApplicationDataMutationLedgerReconcileRequest
{
    public string LedgerId { get; set; } = string.Empty;

    public string TargetStatus { get; set; } = string.Empty;

    public int? ExternalAffectedRows { get; set; }

    public string BusinessEvidence { get; set; } = string.Empty;
}

public sealed record ApplicationDataMutationLedgerResponse(
    string LedgerId,
    string Operation,
    string RequestHash,
    string Status,
    string ResourceKind,
    string? ResourceId,
    string DataSourceId,
    string ObjectName,
    int? ExpectedAffectedRows,
    int AffectedRows,
    DateTime ReservedAt,
    DateTime? ExecutingAt,
    DateTime? FinalizedAt,
    string? FailureCode,
    string? ErrorMessage,
    string? StatusReason,
    string? ReconciledBy,
    DateTime? ReconciledAt);

public sealed record ApplicationDataMutationLedgerReconcileResponse(
    ApplicationDataMutationLedgerResponse Ledger,
    string ConfirmedStatus,
    int ExternalAffectedRows,
    string BusinessEvidence);

public sealed record ApplicationDataMutationLedgerReservation(
    string Operation,
    string RequestHash,
    string ResourceKind,
    string? ResourceId,
    string DataSourceId,
    string ObjectName,
    string StatementSummary,
    string StatementHash,
    string Provider,
    int? ExpectedAffectedRows);
