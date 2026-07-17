namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationQueryPlanResolvedNode(
    string Id,
    string Alias,
    ApplicationQueryPlanResolvedResource Resource);
