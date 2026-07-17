namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationQueryPlanJoin(
    string Type,
    string LeftNodeId,
    string LeftFieldResourceId,
    string RightNodeId,
    string RightFieldResourceId);
