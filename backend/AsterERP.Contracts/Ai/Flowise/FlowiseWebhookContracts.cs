namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseWebhookListenerRegistrationDto
{
    public string ListenerId { get; set; } = string.Empty;

    public string ChatflowId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

public sealed class FlowiseWebhookTriggerRequest
{
    public string? Question { get; set; }

    public string? ChatId { get; set; }

    public string? SessionId { get; set; }

    public string InputJson { get; set; } = "{}";
}

public sealed class FlowiseWebhookTriggerResponse
{
    public string ChatflowId { get; set; } = string.Empty;

    public string ListenerId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string TraceId { get; set; } = string.Empty;
}
