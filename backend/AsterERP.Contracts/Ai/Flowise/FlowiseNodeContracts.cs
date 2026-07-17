namespace AsterERP.Contracts.Ai.Flowise;

public sealed class FlowiseNodeDefinitionDto
{
    public string Name { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public string Category { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Icon { get; set; }

    public IReadOnlyList<string> BaseClasses { get; set; } = [];

    public IReadOnlyList<string> Tags { get; set; } = [];

    public IReadOnlyList<FlowiseNodeAnchorDto> InputAnchors { get; set; } = [];

    public IReadOnlyList<FlowiseNodeAnchorDto> OutputAnchors { get; set; } = [];

    public IReadOnlyList<FlowiseNodeInputParamDto> InputParams { get; set; } = [];
}
