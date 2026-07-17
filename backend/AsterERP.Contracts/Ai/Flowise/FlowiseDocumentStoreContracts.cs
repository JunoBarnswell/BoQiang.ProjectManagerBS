namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseDocumentStoreDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Status { get; set; } = "Enabled";

    public string? WorkspaceId { get; set; }

    public int FileCount { get; set; }

    public int ChunkCount { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class FlowiseDocumentStoreFileDto
{
    public string Id { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public string LoaderType { get; set; } = string.Empty;

    public string LoaderConfigJson { get; set; } = "{}";

    public string Status { get; set; } = "Pending";

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseDocumentStoreChunkDto
{
    public string Id { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string? DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public string Content { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public int TokenCount { get; set; }
}

public sealed class FlowiseVectorStoreConfigDto
{
    public string Id { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string VectorProvider { get; set; } = string.Empty;

    public string EmbeddingProvider { get; set; } = string.Empty;

    public string? RecordManagerProvider { get; set; }

    public string VectorStoreConfigJson { get; set; } = "{}";
}

public sealed class FlowiseDocumentStoreUpsertRequest
{
    public string StoreId { get; set; } = string.Empty;

    public string? LoaderId { get; set; }

    public string? ChatflowId { get; set; }

    public bool ReplaceExisting { get; set; }

    public string FlowData { get; set; } = "{}";

    public string OverrideConfigJson { get; set; } = "{}";
}

public sealed class FlowiseDocumentStoreUpsertHistoryDto
{
    public string Id { get; set; } = string.Empty;

    public string StoreId { get; set; } = string.Empty;

    public string? LoaderId { get; set; }

    public string? ChatflowId { get; set; }

    public string Status { get; set; } = "Completed";

    public int ProcessedCount { get; set; }

    public int AddedCount { get; set; }

    public int ReplacedCount { get; set; }

    public int SkippedCount { get; set; }

    public string? ErrorMessage { get; set; }

    public string RequestJson { get; set; } = "{}";

    public string ResultJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseDocumentStoreQueryRequest
{
    public string StoreId { get; set; } = string.Empty;

    public string Query { get; set; } = string.Empty;

    public int Limit { get; set; } = 10;
}

public sealed class FlowiseDocumentStoreQueryResultDto
{
    public string TraceId { get; set; } = string.Empty;

    public IReadOnlyList<FlowiseDocumentStoreChunkDto> Chunks { get; set; } = [];
}
