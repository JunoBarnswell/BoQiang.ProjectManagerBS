namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseDatasetSaveRequest
{
    public string DatasetKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }

    public string? Status { get; set; }

    public FlowiseDatasetSchemaDto Schema { get; set; } = new();

    public string? AdvancedMetadataJson { get; set; }
}
