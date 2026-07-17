using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.System.Messaging;

[SugarTable("system_message_send_logs")]
public sealed class SystemMessageSendLogEntity : EntityBase
{
    public string Channel { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? MaskedTarget { get; set; }

    public string TraceId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? CorrelationId { get; set; }

    public string Result { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? ErrorSummary { get; set; }

    public long DurationMs { get; set; }
}
