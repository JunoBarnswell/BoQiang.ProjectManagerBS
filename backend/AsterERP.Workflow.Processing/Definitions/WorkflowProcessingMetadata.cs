namespace AsterERP.Workflow.Processing.Definitions;

public sealed class WorkflowProcessingMetadata
{
    public string Version { get; set; } = "1.0";

    public string? Description { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public Dictionary<string, string> Tags { get; set; } = [];
}
