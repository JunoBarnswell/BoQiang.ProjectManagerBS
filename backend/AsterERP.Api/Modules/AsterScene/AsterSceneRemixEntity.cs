using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_remix")]
public sealed class AsterSceneRemixEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string SourceWorkId { get; set; } = string.Empty;

    public string SourceProjectId { get; set; } = string.Empty;

    public string TargetProjectId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string Status { get; set; } = "Completed";

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
