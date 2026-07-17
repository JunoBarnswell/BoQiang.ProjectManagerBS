namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceViewResponse(
    string Id,
    string ViewName,
    string? SchemaName,
    string Alias,
    string ObjectCode,
    string Status,
    string Sql,
    string? Remark,
    DateTime CreatedTime,
    DateTime? UpdatedTime,
    DateTime? LastValidatedAt,
    string? LastValidationStatus,
    string? LastValidationMessage);
