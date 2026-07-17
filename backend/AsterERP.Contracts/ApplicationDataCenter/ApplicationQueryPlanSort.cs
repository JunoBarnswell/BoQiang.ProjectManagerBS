namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanSort(string FieldResourceId, string Direction = "asc", string? NodeId = null);
