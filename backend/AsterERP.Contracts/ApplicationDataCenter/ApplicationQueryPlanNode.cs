namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanNode(
    string Id,
    string ResourceId,
    string Alias,
    string Kind = "table");
