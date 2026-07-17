using AsterERP.Contracts.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record FlowiseExecutionContext(
    string? Question,
    string ResourceId,
    string? ChatId,
    string? SessionId,
    IReadOnlyList<FlowiseChatHistoryMessageDto> ChatHistory,
    IReadOnlyDictionary<string, object?> Form,
    IReadOnlyDictionary<string, object?> Webhook,
    FlowiseHumanInputResumeRequest? HumanInput = null)
{
    private readonly Dictionary<string, int> loopCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> flowState = new(StringComparer.OrdinalIgnoreCase);

    public int CurrentLoopCount => loopCounts.Count == 0 ? 0 : loopCounts.Values.Max();

    public string? NormalizedChatId => string.IsNullOrWhiteSpace(ChatId) ? null : ChatId.Trim();

    public string? NormalizedSessionId => string.IsNullOrWhiteSpace(SessionId) ? NormalizedChatId : SessionId.Trim();

    public void RegisterLoopExecution(string nodeId)
    {
        loopCounts[nodeId] = loopCounts.TryGetValue(nodeId, out var count) ? count + 1 : 1;
    }

    public void SetFlowState(string key, object? value)
    {
        flowState[key] = value;
    }

    public IReadOnlyDictionary<string, object?> SnapshotFlowState() =>
        new Dictionary<string, object?>(flowState, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, object?> BuildFlowVariables()
    {
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["chatflowId"] = ResourceId,
            ["chatId"] = NormalizedChatId,
            ["sessionId"] = NormalizedSessionId,
            ["input"] = Question ?? string.Empty,
            ["state"] = SnapshotFlowState()
        };
    }

    public static FlowiseExecutionContext FromRequest(FlowiseExecutionStartRequest request) =>
        new(
            request.Question,
            request.ResourceId,
            request.ChatId,
            request.SessionId,
            request.ChatHistory
                .Where(item => !string.IsNullOrWhiteSpace(item.Content))
                .Select(item => new FlowiseChatHistoryMessageDto
                {
                    Content = item.Content.Trim(),
                    CreatedTime = item.CreatedTime,
                    Role = string.IsNullOrWhiteSpace(item.Role) ? "user" : item.Role.Trim()
                })
                .TakeLast(20)
                .ToList(),
            NormalizeForm(request.Form),
            NormalizeForm(request.Webhook),
            request.HumanInput);

    public static FlowiseExecutionContext FromQuestion(string? question) =>
        new(question, string.Empty, null, null, [], new Dictionary<string, object?>(), new Dictionary<string, object?>());

    private static IReadOnlyDictionary<string, object?> NormalizeForm(Dictionary<string, object?>? form) =>
        form is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(form, StringComparer.OrdinalIgnoreCase);
}
