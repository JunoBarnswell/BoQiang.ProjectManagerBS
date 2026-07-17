namespace AsterERP.Contracts.Ai.Flowise.DocumentStores;

public sealed class FlowiseDocumentStoreSaveRequest
{
    public string StoreKey { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }

    public string? Status { get; set; }

    public FlowiseDocumentStoreLoaderConfigDto LoaderConfig { get; set; } = new();

    public string? AdvancedMetadataJson { get; set; }
}
