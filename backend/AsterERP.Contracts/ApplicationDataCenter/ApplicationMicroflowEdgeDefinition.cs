namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationMicroflowEdgeDefinition
{
    public string Id { get; set; } = string.Empty;

    public string SourceNodeId { get; set; } = string.Empty;

    public string TargetNodeId { get; set; } = string.Empty;

    public string? Condition { get; set; }
}
