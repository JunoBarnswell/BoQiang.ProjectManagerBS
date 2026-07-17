namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationQueryPlanRequest
{
    public string DataSourceId { get; set; } = string.Empty;
    public IReadOnlyList<ApplicationQueryPlanNode> Nodes { get; set; } = [];
    public IReadOnlyList<ApplicationQueryPlanJoin> Joins { get; set; } = [];
    public IReadOnlyList<ApplicationQueryPlanColumn> Columns { get; set; } = [];
    public IReadOnlyList<ApplicationQueryPlanFilter> Filters { get; set; } = [];
    public IReadOnlyList<ApplicationQueryPlanGroupBy> GroupBy { get; set; } = [];
    public IReadOnlyList<ApplicationQueryPlanFilter> Having { get; set; } = [];
    public IReadOnlyList<ApplicationQueryPlanSort> Sorts { get; set; } = [];
    public ApplicationQueryPlanPage Page { get; set; } = new();
    public IReadOnlyList<ApplicationQueryPlanParameter> Parameters { get; set; } = [];
    public string AccessMode { get; set; } = ApplicationQueryPlanAccessMode.ReadOnly;
    public bool RiskConfirmed { get; set; }
    public string? RiskConfirmationId { get; set; }
    public string? AuditId { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int RowLimit { get; set; } = 100;
    public string? WriteOperation { get; set; }
    public IReadOnlyDictionary<string, string> WriteValues { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    public int? ExpectedAffectedRows { get; set; }
}
