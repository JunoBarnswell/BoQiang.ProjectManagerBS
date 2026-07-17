namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseRuntimeEdge
{
    public string Id { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? SourceHandle { get; set; }

    public string Target { get; set; } = string.Empty;

    public string? TargetHandle { get; set; }
}
