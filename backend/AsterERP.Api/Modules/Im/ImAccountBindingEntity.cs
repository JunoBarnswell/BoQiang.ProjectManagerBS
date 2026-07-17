using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Im;

[SugarTable("im_account_bindings")]
public sealed class ImAccountBindingEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public string ImAccountId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Status { get; set; } = "Enabled";

    public DateTime BoundAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSyncedAt { get; set; } = DateTime.UtcNow;
}
