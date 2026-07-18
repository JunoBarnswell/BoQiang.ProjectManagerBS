using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.ProjectManagement;

/// <summary>
/// ProjectManagement owns the business association to an IM conversation. IM owns the
/// conversation, participants and messages themselves.
/// </summary>
[SugarTable("pm_im_conversation_links")]
public sealed class ProjectManagementImConversationLinkEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;
    public string AppCode { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? TaskId { get; set; }

    public string ConversationKey { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ConversationId { get; set; }

    public string MemberSource { get; set; } = "ProjectMembers";
    public string Status { get; set; } = "Provisioning";

    [SugarColumn(IsNullable = true)]
    public string? LastSyncError { get; set; }

    public long VersionNo { get; set; } = 1;
}
