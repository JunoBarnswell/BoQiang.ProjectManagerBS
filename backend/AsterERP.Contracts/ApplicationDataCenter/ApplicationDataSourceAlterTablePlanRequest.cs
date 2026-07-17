namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceAlterTablePlanRequest(
    string PlanHash,
    ApplicationDataSourceAlterTableRequest Table,
    bool Confirmed);
