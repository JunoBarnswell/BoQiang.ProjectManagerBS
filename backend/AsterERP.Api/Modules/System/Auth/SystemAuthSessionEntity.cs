using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Auth;

[SugarTable("system_auth_sessions")]
public sealed class SystemAuthSessionEntity : EntityBase
{
    public string UserId { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public int SessionVersion { get; set; } = 1;

    [SugarColumn(IsNullable = true)]
    public string? CsrfSecretHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? RevokedAt { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientIp { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserAgent { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? LastSeenTime { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CurrentTenantId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? CurrentAppCode { get; set; }

    [SugarColumn(IsNullable = true)]
    public DateTime? WorkspaceSwitchedAt { get; set; }
}
