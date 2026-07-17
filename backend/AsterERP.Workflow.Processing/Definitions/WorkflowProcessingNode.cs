namespace AsterERP.Workflow.Processing.Definitions;

public sealed class WorkflowProcessingNode
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NodeType { get; set; } = "Task";

    public bool IsEntry { get; set; }

    public bool IsExit { get; set; }

    public List<string> DependsOn { get; set; } = [];

    public Dictionary<string, string> Metadata { get; set; } = [];
}
