namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseMcpServerConfigDto
{
    public string ChatflowId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string EndpointPath { get; set; } = string.Empty;

    public bool Enabled { get; set; }

    public bool HasExistingConfig { get; set; }

    public string Token { get; set; } = string.Empty;

    public string ToolName { get; set; } = string.Empty;
}

public sealed class FlowiseMcpServerUpsertRequest
{
    public string Description { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string ToolName { get; set; } = string.Empty;
}
