using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record FlowiseRuntimeNode
{
    public string Id { get; set; } = string.Empty;

    public string NodeType { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? ParentId { get; set; }

    public IReadOnlyDictionary<string, JsonElement> Data { get; set; } = new Dictionary<string, JsonElement>();
}
