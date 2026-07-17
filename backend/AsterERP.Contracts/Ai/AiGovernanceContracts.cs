namespace AsterERP.Contracts.Ai;

public class AiUsageQuery
{
    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public string? UserId { get; set; }

    public string? ProviderCode { get; set; }

    public string? ModelCode { get; set; }
}

public class AiSecuritySettingsDto
{
    public bool RequireToolConfirmation { get; set; } = true;

    public int MaxParallelAgents { get; set; } = 3;

    public int MaxInputCharacters { get; set; } = 16000;

    public int MaxContextMessages { get; set; } = 40;

    public bool AllowReasoningDisplay { get; set; } = true;

    public string MultiAgentFailurePolicy { get; set; } = "SkipFailed";
}

public sealed class AiSecuritySettingsUpdateRequest : AiSecuritySettingsDto
{
}
