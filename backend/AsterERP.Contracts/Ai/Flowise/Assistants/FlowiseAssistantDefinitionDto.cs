namespace AsterERP.Contracts.Ai.Flowise.Assistants;

public sealed class FlowiseAssistantDefinitionDto
{
    public string? Instructions { get; set; }

    public string? Model { get; set; }

    public string? ResponseFormat { get; set; }

    public IReadOnlyList<string> FileIds { get; set; } = [];

    public IReadOnlyList<string> Tools { get; set; } = [];

    public decimal? Temperature { get; set; }

    public decimal? TopP { get; set; }
}
