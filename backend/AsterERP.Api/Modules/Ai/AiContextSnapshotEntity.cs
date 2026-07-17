using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_context_snapshots")]
public sealed class AiContextSnapshotEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public int FromSeq { get; set; }

    public int ToSeq { get; set; }

    public string Summary { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ModelConfigId { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public string SnapshotType { get; set; } = "Manual";
}
