using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_index_tasks")]
public sealed class AiKnowledgeIndexTaskEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DocumentId { get; set; }

    public string Status { get; set; } = "Pending";

    public int Progress { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedTime { get; set; }
}
