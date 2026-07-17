namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceSchemaChangePlanRequest(
    string PlanHash,
    ApplicationDataSourceCreateTableRequest Table,
    bool Confirmed);
