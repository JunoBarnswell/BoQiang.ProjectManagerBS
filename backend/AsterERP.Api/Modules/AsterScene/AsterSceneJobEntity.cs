using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_job")]
public sealed class AsterSceneJobEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ProjectId { get; set; }

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? AssetId { get; set; }

    public string JobCode { get; set; } = string.Empty;

    public string JobType { get; set; } = string.Empty;

    public string Status { get; set; } = "Pending";

    public int ProgressPercent { get; set; }

    public int Attempts { get; set; }

    public int MaxAttempts { get; set; } = 3;

    [SugarColumn(IsNullable = true)]
    public string? IdempotencyKey { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? InputJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? OutputJson { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? StartedTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? FinishedTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? CanceledTime { get; set; }
}
