namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationQueryPlanResolvedModel
{
    public ApplicationQueryPlanResolvedModel(IReadOnlyList<ApplicationQueryPlanResolvedNode> nodes)
    {
        Nodes = nodes;
        ById = nodes.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ApplicationQueryPlanResolvedNode> Nodes { get; }

    public IReadOnlyDictionary<string, ApplicationQueryPlanResolvedNode> ById { get; }
}
