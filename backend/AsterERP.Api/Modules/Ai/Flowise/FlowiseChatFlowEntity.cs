using AsterERP.Api.Modules.Ai;
using AsterERP.Domain.Common;
using SqlSugar;

namespace AsterERP.Api.Modules.Ai.Flowise;

[SugarTable("ai_flowise_chat_flows")]
public sealed class FlowiseChatFlowEntity : EntityBase, IFlowiseSharedResourceEntity
{
    public string TenantId { get; set; } = string.Empty;

    public string AppCode { get; set; } = string.Empty;

    public string OwnerUserId { get; set; } = string.Empty;

    [SugarColumn(IsNullable = true)]
    public string? WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    [SugarColumn(ColumnDataType = "TEXT")]
    public string FlowData { get; set; } = "{}";

    public string Type { get; set; } = "CHATFLOW";

    public bool Deployed { get; set; }

    public bool IsPublic { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Apikeyid { get; set; }

    [SugarColumn(IsNullable = true)]
    public string? Category { get; set; }

    [SugarColumn(ColumnDataType = "TEXT")]
    public string MetadataJson { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ChatbotConfig { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string ApiConfig { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string Analytic { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string SpeechToText { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string TextToSpeech { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string FollowUpPrompts { get; set; } = "{}";

    [SugarColumn(ColumnDataType = "TEXT")]
    public string McpServerConfig { get; set; } = "{}";

    [SugarColumn(IsNullable = true)]
    public string? WebhookSecretCipherText { get; set; }

    public bool WebhookSecretConfigured { get; set; }
}
