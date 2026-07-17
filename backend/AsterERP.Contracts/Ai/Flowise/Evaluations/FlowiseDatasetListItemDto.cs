namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseDatasetListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string DatasetKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }

    public string Status { get; set; } = "Enabled";

    public FlowiseDatasetSchemaDto Schema { get; set; } = new();

    public string AdvancedMetadataJson { get; set; } = "{}";

    public int RowCount { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}
