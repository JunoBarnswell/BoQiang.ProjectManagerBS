using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Users;

[SugarTable("system_user_employments")]
public sealed class SystemUserEmploymentEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string TenantId { get; set; } = "tenant-system";

    public string AppCode { get; set; } = "SYSTEM";

    public string DeptId { get; set; } = string.Empty;

    public string PositionId { get; set; } = string.Empty;

    public string EmploymentName { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public string Status { get; set; } = "Enabled";

    public int SortOrder { get; set; }
}
