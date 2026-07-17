namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataSourceSchemaChangePlanResponse(
    string PlanId,
    string PlanHash,
    string DataSourceId,
    string Provider,
    string Operation,
    string Target,
    string SqlPreview,
    IReadOnlyList<string> Risks,
    string RiskLevel,
    bool RequiresLock,
    int? EstimatedAffectedRows,
    IReadOnlyList<string> Dependencies,
    bool RequiresConfirmation,
    bool Reversible,
    DateTime CreatedAt)
{
    public IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> BeforeColumns { get; init; } = [];
    public IReadOnlyList<ApplicationDataSourceCreateTableColumnRequest> AfterColumns { get; init; } = [];
}
