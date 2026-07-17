namespace AsterERP.Contracts.ApplicationDataCenter;

/// <summary>Read-only inventory entry for a data source whose provider was retired.</summary>
public sealed record ApplicationDataSourceProviderMigrationItemResponse(
    string Id,
    string ObjectCode,
    string ObjectName,
    string RetiredProvider,
    string Status,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    string Diagnostic);
