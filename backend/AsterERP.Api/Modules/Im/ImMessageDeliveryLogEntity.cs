using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Im;

[SugarTable("im_message_delivery_logs")]
public sealed class ImMessageDeliveryLogEntity : EntityBase
{
    public string TenantId { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? MessageId { get; set; }

    public string Channel { get; set; } = "SignalR";

    public string TargetUserId { get; set; } = string.Empty;

    public string Result { get; set; } = "Pending";

    [SugarColumn(IsNullable = true)]
    public string? ErrorMessage { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? TraceId { get; set; }
}
