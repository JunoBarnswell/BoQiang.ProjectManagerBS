namespace AsterERP.Contracts.Ai;

public sealed class AiMessageDto
{
    public string Id { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string? RunId { get; set; }

    public string? AgentProfileId { get; set; }

    public string Role { get; set; } = string.Empty;

    public int Seq { get; set; }

    public string Content { get; set; } = string.Empty;

    public string? ReasoningContent { get; set; }

    public string? MetadataJson { get; set; }

    public string? Status { get; set; }

    public string? FinishReason { get; set; }

    public int TokenCount { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class AiMessageFeedbackRequest
{
    public string Rating { get; set; } = string.Empty;

    public string? Comment { get; set; }
}

public sealed class AiContextSnapshotDto
{
    public string Id { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public int FromSeq { get; set; }

    public int ToSeq { get; set; }

    public string Summary { get; set; } = string.Empty;

    public int TotalTokens { get; set; }

    public DateTime CreatedTime { get; set; }
}
