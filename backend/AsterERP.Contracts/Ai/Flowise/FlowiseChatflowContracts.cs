namespace AsterERP.Contracts.Ai.Flowise;

public static class FlowiseChatflowTypes
{
    public const string Chatflow = "CHATFLOW";
    public const string Agentflow = "AGENTFLOW";
    public const string Multiagent = "MULTIAGENT";
    public const string Assistant = "ASSISTANT";
}

public sealed class FlowiseChatflowQuery
{
    public string? Type { get; set; }

    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public string? WorkspaceId { get; set; }

    public string? Category { get; set; }

    public bool? Deployed { get; set; }
}

public sealed class FlowiseChatflowDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string FlowData { get; set; } = "{}";

    public string Type { get; set; } = FlowiseChatflowTypes.Chatflow;

    public bool Deployed { get; set; }

    public bool IsPublic { get; set; }

    public string? Apikeyid { get; set; }

    public string? Category { get; set; }

    public string MetadataJson { get; set; } = "{}";

    public string? WorkspaceId { get; set; }

    public string ChatbotConfig { get; set; } = "{}";

    public string ApiConfig { get; set; } = "{}";

    public string Analytic { get; set; } = "{}";

    public string SpeechToText { get; set; } = "{}";

    public string TextToSpeech { get; set; } = "{}";

    public string FollowUpPrompts { get; set; } = "{}";

    public string McpServerConfig { get; set; } = "{}";

    public bool WebhookSecretConfigured { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }
}

public sealed class FlowiseChatflowUpsertRequest
{
    public string Name { get; set; } = string.Empty;

    public string FlowData { get; set; } = "{}";

    public string Type { get; set; } = FlowiseChatflowTypes.Chatflow;

    public bool Deployed { get; set; }

    public bool IsPublic { get; set; }

    public string? Apikeyid { get; set; }

    public string? Category { get; set; }

    public string? MetadataJson { get; set; }

    public string? WorkspaceId { get; set; }

    public string? ChatbotConfig { get; set; }

    public string? ApiConfig { get; set; }

    public string? Analytic { get; set; }

    public string? SpeechToText { get; set; }

    public string? TextToSpeech { get; set; }

    public string? FollowUpPrompts { get; set; }

    public string? McpServerConfig { get; set; }

    public string? WebhookSecret { get; set; }
}

public sealed class FlowiseChatflowConfigurationRequest
{
    public string? ChatbotConfig { get; set; }

    public string? ApiConfig { get; set; }

    public string? Analytic { get; set; }

    public string? SpeechToText { get; set; }

    public string? TextToSpeech { get; set; }

    public string? FollowUpPrompts { get; set; }

    public string? McpServerConfig { get; set; }

    public string? WebhookSecret { get; set; }
}

public sealed class FlowiseChatflowDomainsRequest
{
    public string? ChatbotConfig { get; set; }
}

public sealed class FlowiseScheduleStatusDto
{
    public bool IsScheduled { get; set; }

    public bool Enabled { get; set; }

    public string? CronExpression { get; set; }

    public string? Timezone { get; set; }

    public string ScheduleInputMode { get; set; } = "text";

    public string? DefaultInput { get; set; }

    public string? DefaultFormJson { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime? LastRunAt { get; set; }

    public DateTime? NextRunAt { get; set; }
}

public sealed class FlowiseScheduleTriggerLogDto
{
    public string Id { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? ExecutionId { get; set; }

    public string? OutputJson { get; set; }

    public DateTime ScheduledAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public sealed class FlowiseScheduleLogQuery
{
    public int PageIndex { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string? Status { get; set; }
}
