namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceSqlitePathApprovalResponse(
    string Id,
    string DataSourceId,
    string Path,
    string Reason,
    string Status,
    string RequestedBy,
    DateTime RequestedAt,
    string? ApprovedBy,
    DateTime? ApprovedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt);
