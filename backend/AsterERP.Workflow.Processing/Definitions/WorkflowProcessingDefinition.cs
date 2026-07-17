namespace AsterERP.Workflow.Processing.Definitions;

public sealed class WorkflowProcessingDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public WorkflowProcessingMetadata Metadata { get; set; } = new();

    public List<WorkflowProcessingNode> Nodes { get; set; } = [];

    public List<WorkflowProcessingEdge> Edges { get; set; } = [];

    public bool RequiresAcyclicGraph { get; set; } = true;
}
