namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class LlmNodeResult
{
    public int ExecutionIndex { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public IReadOnlyList<LlmMessageDto> Messages { get; set; } = [];

    public string Content { get; set; } = string.Empty;

    public string ReturnResponseAs { get; set; } = "userMessage";

    public DateTime StartedAt { get; set; }

    public DateTime CompletedAt { get; set; }

    public IReadOnlyDictionary<string, object?> StructuredOutput { get; set; } = new Dictionary<string, object?>();
}
