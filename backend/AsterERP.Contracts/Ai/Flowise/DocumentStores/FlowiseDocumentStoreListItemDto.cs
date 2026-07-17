namespace AsterERP.Contracts.Ai.Flowise.DocumentStores;

public sealed class FlowiseDocumentStoreListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string StoreKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? WorkspaceName { get; set; }

    public string? Category { get; set; }

    public string Status { get; set; } = "Enabled";

    public FlowiseDocumentStoreLoaderConfigDto LoaderConfig { get; set; } = new();

    public string AdvancedMetadataJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}
