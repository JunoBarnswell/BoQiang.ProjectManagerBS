using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_chat_messages")]
public sealed class FlowiseChatMessageEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string ResourceId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ExecutionId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ChatId { get; set; }

    public string Role { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string SourceDocumentsJson { get; set; } = "[]";

    public string FileUploadsJson { get; set; } = "[]";

    public string AgentReasoningJson { get; set; } = "[]";

    public string AgentExecutedDataJson { get; set; } = "[]";

    public string UsedToolsJson { get; set; } = "[]";

    public string ArtifactsJson { get; set; } = "[]";

    [SugarColumn(IsNullable = true)]
    public string? ActionJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FollowUpPromptsJson { get; set; }
}
