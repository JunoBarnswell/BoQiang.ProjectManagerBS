namespace AsterERP.Contracts.Ai;

public sealed class AiDataCenterAssistantIntentResponse
{
    public string ConversationId { get; set; } = string.Empty;

    public string? RunId { get; set; }

    public string? UserMessageId { get; set; }

    public string? AssistantMessageId { get; set; }

    public string ModelConfigId { get; set; } = string.Empty;

    public string ReplyText { get; set; } = string.Empty;

    public IReadOnlyList<AiDataCenterAssistantToolIntentDto> ToolIntents { get; set; } = [];
}
