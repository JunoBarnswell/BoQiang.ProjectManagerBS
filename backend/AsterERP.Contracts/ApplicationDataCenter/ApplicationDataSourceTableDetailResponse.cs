namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceTableDetailResponse(
    ApplicationDataSourceTableResponse Table,
    IReadOnlyList<ApplicationDataSourceColumnResponse> Columns);
