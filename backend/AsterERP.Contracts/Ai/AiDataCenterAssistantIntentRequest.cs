namespace AsterERP.Contracts.Ai;

public sealed class AiDataCenterAssistantIntentRequest
{
    public string? ConversationId { get; set; }

    public string DataSourceId { get; set; } = string.Empty;

    public string? DataSourceName { get; set; }

    public string? ModelConfigId { get; set; }

    public string? SelectedTable { get; set; }

    public string Content { get; set; } = string.Empty;
}
