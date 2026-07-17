using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai;

[SugarTable("ai_task_process_states")]
public sealed class AiTaskProcessStateEntity : EntityBase, IAiWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string PlanId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public string ProcessStatus { get; set; } = "FrameworkUnavailable";

    [SugarColumn(IsNullable = true)]
    public string? ResumeToken { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? StateJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }
}
