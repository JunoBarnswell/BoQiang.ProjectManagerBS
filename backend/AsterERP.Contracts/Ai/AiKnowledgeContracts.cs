namespace AsterERP.Contracts.Ai;

public sealed class AiKnowledgeSourceDto
{
    public string Id { get; set; } = string.Empty;

    public string SourceCode { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string SourceType { get; set; } = "Document";

    public string Status { get; set; } = "Disabled";

    public DateTime CreatedTime { get; set; }
}

public sealed class AiKnowledgeSourceUpsertRequest
{
    public string SourceCode { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;

    public string SourceType { get; set; } = "Document";

    public string? Description { get; set; }
}

public sealed class AiKnowledgeDocumentDto
{
    public string Id { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string DocumentName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string IndexStatus { get; set; } = "Pending";

    public int ChunkCount { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AiKnowledgeSearchRequest
{
    public string Query { get; set; } = string.Empty;

    public string? SourceId { get; set; }

    public int TopK { get; set; } = 10;
}

public sealed class AiKnowledgeSearchResponse
{
    public IReadOnlyList<AiKnowledgeSearchHitDto> Hits { get; set; } = [];
}

public sealed class AiKnowledgeSearchHitDto
{
    public string DocumentId { get; set; } = string.Empty;

    public string ChunkId { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public decimal Score { get; set; }
}
