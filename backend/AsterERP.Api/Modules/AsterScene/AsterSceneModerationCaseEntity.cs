using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_moderation_case")]
public sealed class AsterSceneModerationCaseEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ProjectId { get; set; }

    public string ReporterUserId { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? Detail { get; set; }

    public string Status { get; set; } = "Open";

    [SugarColumn(IsNullable = true)]
    public string? Decision { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DecisionNote { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DecidedBy { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? DecidedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
