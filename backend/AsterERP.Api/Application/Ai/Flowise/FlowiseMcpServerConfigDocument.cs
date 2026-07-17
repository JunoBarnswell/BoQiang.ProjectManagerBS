namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseMcpServerConfigDocument
{
    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public string Token { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;
}
