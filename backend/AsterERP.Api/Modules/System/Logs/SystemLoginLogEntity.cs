using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Logs;

[SugarTable("system_login_logs")]
public sealed class SystemLoginLogEntity : EntityBase
{
    public string TraceId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserDisplayName { get; set; }

    public DateTime LoginTime { get; set; } = DateTime.UtcNow;

    public string LoginResult { get; set; } = "Success";

    public bool IsSuccess { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? FailureReason { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientIp { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserAgent { get; set; }
}
