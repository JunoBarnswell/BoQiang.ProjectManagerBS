using AsterERP.Shared;

namespace AsterERP.Contracts.Ai;

public sealed class AiObservabilitySummaryDto
{
    public int RequestCount { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }

    public decimal CostAmount { get; set; }

    public int RunCount { get; set; }

    public int RunningRunCount { get; set; }

    public int ToolExecutionCount { get; set; }

    public int FailedToolExecutionCount { get; set; }
}

public sealed class AiObservabilityTrendPointDto
{
    public string Bucket { get; set; } = string.Empty;

    public int RequestCount { get; set; }

    public int SuccessCount { get; set; }

    public int FailedCount { get; set; }

    public int TotalTokens { get; set; }

    public decimal CostAmount { get; set; }
}

public sealed class AiRunListItemDto
{
    public string Id { get; set; } = string.Empty;

    public string? ConversationId { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int TotalTokens { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public sealed class AiRunDetailDto
{
    public string Id { get; set; } = string.Empty;

    public string? ConversationId { get; set; }

    public string Mode { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int TotalTokens { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public IReadOnlyList<AiMessageDto> Messages { get; set; } = [];

    public IReadOnlyList<AiTaskPlanDto> TaskPlans { get; set; } = [];

    public IReadOnlyList<AiToolInvocationDto> ToolExecutions { get; set; } = [];
}

public sealed class AiFailureSummaryDto
{
    public string ErrorCode { get; set; } = string.Empty;

    public string ErrorMessage { get; set; } = string.Empty;

    public int Count { get; set; }
}

public sealed class AiObservabilityRunQuery : AiUsageQuery
{
    public string? Status { get; set; }

    public string? Mode { get; set; }
}

public sealed class AiToolExecutionQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? RunId { get; set; }

    public string? ToolCode { get; set; }

    public string? Status { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }
}
