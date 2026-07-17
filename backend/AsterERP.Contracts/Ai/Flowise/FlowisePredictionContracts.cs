namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowisePredictionRequest
{
    public string ResourceId { get; set; } = string.Empty;

    public string Question { get; set; } = string.Empty;

    public string? ChatId { get; set; }

    public string? SessionId { get; set; }

    public Dictionary<string, object?>? Form { get; set; }

    public IReadOnlyList<FlowiseFileUploadDto> Uploads { get; set; } = [];
}

public sealed class FlowisePredictionListQuery
{
    public string ResourceId { get; set; } = string.Empty;

    public string? ChatId { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class FlowiseSourceDocumentDto
{
    public string Content { get; set; } = string.Empty;

    public string MetadataJson { get; set; } = "{}";

    public decimal? Score { get; set; }

    public string? SourceId { get; set; }
}

public sealed class FlowiseFileUploadDto
{
    public string Data { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Mime { get; set; } = string.Empty;
}

public sealed class FlowiseChatHistoryMessageDto
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseUsedToolDto
{
    public string Tool { get; set; } = string.Empty;

    public string InputJson { get; set; } = "{}";

    public string OutputJson { get; set; } = "{}";
}

public sealed class FlowiseAgentReasoningDto
{
    public string AgentName { get; set; } = string.Empty;

    public string NodeName { get; set; } = string.Empty;

    public string Instructions { get; set; } = string.Empty;

    public IReadOnlyList<string> Messages { get; set; } = [];

    public IReadOnlyList<FlowiseUsedToolDto> UsedTools { get; set; } = [];

    public IReadOnlyList<FlowiseSourceDocumentDto> SourceDocuments { get; set; } = [];

    public string StateJson { get; set; } = "{}";

    public string ArtifactsJson { get; set; } = "[]";

    public string? NextAgent { get; set; }
}

public sealed class FlowiseAgentExecutedNodeDto
{
    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public IReadOnlyList<string> PreviousNodeIds { get; set; } = [];

    public string DataJson { get; set; } = "{}";
}

public sealed class FlowiseChatMessageDto
{
    public string Id { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? ExecutionId { get; set; }

    public string? ChatId { get; set; }

    public IReadOnlyList<FlowiseSourceDocumentDto> SourceDocuments { get; set; } = [];

    public IReadOnlyList<FlowiseFileUploadDto> FileUploads { get; set; } = [];

    public IReadOnlyList<FlowiseAgentReasoningDto> AgentReasoning { get; set; } = [];

    public IReadOnlyList<FlowiseAgentExecutedNodeDto> AgentExecutedData { get; set; } = [];

    public IReadOnlyList<FlowiseUsedToolDto> UsedTools { get; set; } = [];

    public string ArtifactsJson { get; set; } = "[]";

    public string? ActionJson { get; set; }

    public IReadOnlyList<string> FollowUpPrompts { get; set; } = [];

    public FlowiseFeedbackDto? Feedback { get; set; }

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseFeedbackDto
{
    public string Id { get; set; } = string.Empty;

    public string MessageId { get; set; } = string.Empty;

    public string Rating { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class FlowiseFeedbackRequest
{
    public string MessageId { get; set; } = string.Empty;

    public string Rating { get; set; } = string.Empty;

    public string? Reason { get; set; }
}

public sealed class FlowiseLeadDto
{
    public string Id { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public string ContactJson { get; set; } = "{}";

    public DateTime CreatedTime { get; set; }
}

public sealed class FlowiseLeadRequest
{
    public string ResourceId { get; set; } = string.Empty;

    public string ContactJson { get; set; } = "{}";
}

public sealed class FlowiseChatClearRequest
{
    public string ResourceId { get; set; } = string.Empty;

    public string? ChatId { get; set; }
}

public sealed class FlowisePredictionAbortRequest
{
    public string ResourceId { get; set; } = string.Empty;

    public string? ChatId { get; set; }
}

public sealed class FlowisePredictionResponse
{
    public FlowiseExecutionDto Execution { get; set; } = new();

    public FlowiseChatMessageDto Message { get; set; } = new();
}
