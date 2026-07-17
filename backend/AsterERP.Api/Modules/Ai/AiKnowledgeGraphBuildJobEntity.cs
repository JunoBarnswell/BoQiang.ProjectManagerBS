using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_knowledge_graph_build_jobs")]
public sealed class AiKnowledgeGraphBuildJobEntity : EntityBase, IAiOwnedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? SourceId { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int Progress { get; set; }

    public int CreatedCount { get; set; }

    public int UpdatedCount { get; set; }

    public int SkippedCount { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedAt { get; set; }
}
