namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanParameter(string ResourceId, string Name, string Type, object? Value);
