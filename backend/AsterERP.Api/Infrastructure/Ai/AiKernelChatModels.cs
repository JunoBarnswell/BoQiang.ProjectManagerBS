using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class AiKernelChatRequest
{
    public AiModelEndpoint Endpoint { get; init; } = new();

    public IReadOnlyList<Microsoft.SemanticKernel.ChatMessageContent> Messages { get; init; } = [];

    public bool JsonResponse { get; init; }

    public string? AgentName { get; init; }

    public string? UserId { get; init; }

    public IReadOnlyList<string> EnabledFunctionNames { get; init; } = [];

    public Dictionary<string, object?> ExtraParameters { get; init; } = [];
}

public sealed class AiKernelChatChunk
{
    public string? ContentDelta { get; init; }

    public string? FinishReason { get; init; }

    public AiChatUsage? Usage { get; init; }
}

public sealed class AiChatUsage
{
    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int ReasoningTokens { get; set; }

    public int TotalTokens { get; set; }
}
