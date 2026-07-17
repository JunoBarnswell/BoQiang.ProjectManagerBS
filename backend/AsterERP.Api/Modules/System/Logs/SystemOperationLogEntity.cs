using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Logs;

[SugarTable("system_operation_logs")]
public sealed class SystemOperationLogEntity : EntityBase
{
    public string TraceId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? CorrelationId { get; set; }

    public string RequestPath { get; set; } = string.Empty;

    public string RequestMethod { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? RouteDisplayName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ModuleName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? OperationType { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ActionName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? RequestQuery { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ClientIp { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserId { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? UserName { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? ExceptionSummary { get; set; }

    public int StatusCode { get; set; }

    public long DurationMs { get; set; }

    public bool IsSuccess { get; set; }
}
