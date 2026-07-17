namespace AsterERP.Contracts.Ai.Flowise.DocumentStores;

public sealed class FlowiseDocumentStoreLoaderConfigDto
{
    public string? LoaderType { get; set; }

    public string? SourceType { get; set; }

    public int? ChunkSize { get; set; }

    public int? ChunkOverlap { get; set; }

    public string AdvancedConfigJson { get; set; } = "{}";
}
