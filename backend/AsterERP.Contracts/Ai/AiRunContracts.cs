namespace AsterERP.Contracts.Ai;

public sealed class AiChatStreamRequest
{
    public string Content { get; set; } = string.Empty;

    public string? ModelConfigId { get; set; }

    public string? PromptTemplateId { get; set; }

    public string Mode { get; set; } = "Single";

    public string WorkMode { get; set; } = "Ask";

    public string? TaskPlanId { get; set; }

    public IReadOnlyList<string> AgentProfileIds { get; set; } = [];

    public string? CoordinatorAgentProfileId { get; set; }

    public string? ClientMessageId { get; set; }

    public string? IdempotencyKey { get; set; }

    public bool? ThinkingEnabled { get; set; }

    public string? ReasoningEffort { get; set; }

    public decimal? Temperature { get; set; }

    public decimal? TopP { get; set; }

    public int? MaxTokens { get; set; }

    public bool? ToolStreamEnabled { get; set; }

    public bool RequireToolConfirmation { get; set; } = true;

    public IReadOnlyList<string> EnabledToolCodes { get; set; } = [];

    public IReadOnlyList<string> EnabledToolDomains { get; set; } = [];

    public Dictionary<string, object?> ExtraParameters { get; set; } = [];
}

public sealed class AiRunDto
{
    public string Id { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string? UserMessageId { get; set; }

    public string? AssistantMessageId { get; set; }

    public string? ModelConfigId { get; set; }

    public string Mode { get; set; } = "Single";

    public string Status { get; set; } = "Queued";

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public IReadOnlyList<AiRunParticipantDto> Participants { get; set; } = [];
}

public sealed class AiRunParticipantDto
{
    public string Id { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string AgentProfileId { get; set; } = string.Empty;

    public string AgentName { get; set; } = string.Empty;

    public string Status { get; set; } = "Queued";

    public string? DraftMessageId { get; set; }

    public string? ErrorMessage { get; set; }
}

public sealed class AiStreamEventDto
{
    public string Event { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;

    public long Seq { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public object? Data { get; set; }
}
