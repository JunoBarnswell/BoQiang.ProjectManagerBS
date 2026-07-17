using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Users;

[SugarTable("system_users")]
public sealed class SystemUserEntity : EntityBase
{
    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool PasswordResetRequired { get; set; }

    public string PasswordFormatVersion { get; set; } = "v1";

    [SugarColumn(IsNullable = true)]
    public string? PhoneNumber { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Email { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? DeptId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? PositionId { get; set; }

    public bool IsAdmin { get; set; }

    public string Status { get; set; } = "Enabled";
}
