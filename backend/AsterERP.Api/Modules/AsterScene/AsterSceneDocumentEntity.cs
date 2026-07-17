using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_document")]
public sealed class AsterSceneDocumentEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public int Revision { get; set; } = 1;

    public string DocumentJson { get; set; } = "{}";

    public string DocumentHash { get; set; } = string.Empty;

    public bool IsCurrent { get; set; } = true;

    public string SaveSource { get; set; } = "Manual";

    public string SavedBy { get; set; } = string.Empty;

    public DateTime SavedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
