using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_publish_version")]
public sealed class AsterScenePublishVersionEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string PublishCode { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string Status { get; set; } = "Active";

    public int DocumentRevision { get; set; }

    public string DocumentHash { get; set; } = string.Empty;

    public string RuntimeManifestJson { get; set; } = "{}";

    public string EntrySceneId { get; set; } = string.Empty;

    public string Visibility { get; set; } = "Public";

    public string PublishedBy { get; set; } = string.Empty;

    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    [SugarColumn(IsNullable = true)]
    public DateTime? RolledBackAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientMutationId { get; set; }
}
