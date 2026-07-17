namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterSqlScriptAuditSummaryResponse(
    string Id,
    string TraceId,
    string Status,
    string StatementSummary,
    long DurationMs,
    int AffectedRows,
    int ReturnedRows,
    string? ErrorMessage);
