namespace AsterERP.Workflow.Processing.Definitions;

public sealed class WorkflowProcessingEdge
{
    public string Id { get; set; } = string.Empty;

    public string FromNodeId { get; set; } = string.Empty;

    public string ToNodeId { get; set; } = string.Empty;

    public string EdgeType { get; set; } = "Sequence";

    public string? Condition { get; set; }

    public Dictionary<string, string> Metadata { get; set; } = [];
}
