using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_conversations")]
public sealed class AiConversationEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = "Active";

    public bool IsFavorite { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Summary { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ActiveSnapshotId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? LastRunStatus { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastMessageAt { get; set; }
}
