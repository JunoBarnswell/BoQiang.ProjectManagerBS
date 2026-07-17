namespace AsterERP.Contracts.Ai;

public sealed class AiDataCenterAssistantToolIntentDto
{
    public string ToolCode { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "L0";

    public bool RequiresConfirmation { get; set; }

    public Dictionary<string, object?> Arguments { get; set; } = [];

    public string ArgumentsJson { get; set; } = "{}";
}
