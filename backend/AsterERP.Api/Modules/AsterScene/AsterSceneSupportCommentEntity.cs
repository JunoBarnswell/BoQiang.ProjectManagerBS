using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.AsterScene;

[SugarTable("asterscene_support_comment")]
public sealed class AsterSceneSupportCommentEntity : EntityBase, IAsterSceneWorkspaceScopedEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    public string TicketId { get; set; } = string.Empty;

    public string AuthorUserId { get; set; } = string.Empty;

    public string CommentType { get; set; } = "Comment";

    public string Message { get; set; } = string.Empty;

    public string? StatusAfter { get; set; }

    public string? ClientMutationId { get; set; }
}
