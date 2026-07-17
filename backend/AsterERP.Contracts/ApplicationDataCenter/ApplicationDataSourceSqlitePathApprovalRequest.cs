namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceSqlitePathApprovalRequest(
    string DataSourceId,
    string Path,
    string Reason,
    DateTime ExpiresAt);
