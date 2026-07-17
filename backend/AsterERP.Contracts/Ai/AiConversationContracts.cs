namespace AsterERP.Contracts.Ai;

public class AiConversationDto
{
    public string Id { get; set; } = string.Empty;

    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    public bool IsFavorite { get; set; }

    public string? Summary { get; set; }

    public string? LastRunStatus { get; set; }

    public DateTime? LastMessageAt { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AiConversationDetailDto : AiConversationDto
{
    public IReadOnlyList<AiMessageDto> Messages { get; set; } = [];

    public IReadOnlyList<AiContextSnapshotDto> Snapshots { get; set; } = [];
}

public sealed class AiConversationCreateRequest
{
    public string? Title { get; set; }

    public string? ModelConfigId { get; set; }

    public string? PromptTemplateId { get; set; }

    public IReadOnlyList<string> AgentProfileIds { get; set; } = [];
}

public sealed class AiConversationUpdateRequest
{
    public string Title { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }
}

public sealed class AiConversationStatusRequest
{
    public string Status { get; set; } = "Active";
}
