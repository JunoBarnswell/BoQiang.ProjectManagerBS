using System.Text.Json;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Workflows;

/// <summary>
/// Enforces the persisted latest Workflow Business Model envelope. BPMN is a
/// compiled deployment artifact and cannot substitute for this business model.
/// </summary>
public sealed class WorkflowBusinessModelLatestValidator
{
    private const string ContractKind = "WorkflowBusinessModelLatest";

    public void ValidatePersisted(string? extensionJson)
    {
        if (string.IsNullOrWhiteSpace(extensionJson))
        {
            throw MigrationBlocked("Workflow business model is missing.");
        }

        try
        {
            using var document = JsonDocument.Parse(extensionJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasString(root, "version", "latest") ||
                !HasString(root, "kind", ContractKind) ||
                !root.TryGetProperty("businessDesign", out var businessDesign) ||
                businessDesign.ValueKind != JsonValueKind.Object ||
                !HasString(businessDesign, "version", "latest") ||
                !businessDesign.TryGetProperty("selectedNodeId", out var selectedNodeId) ||
                selectedNodeId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(selectedNodeId.GetString()) ||
                !businessDesign.TryGetProperty("nodes", out var nodes) ||
                nodes.ValueKind != JsonValueKind.Array ||
                nodes.GetArrayLength() == 0 ||
                !businessDesign.TryGetProperty("edges", out var edges) ||
                edges.ValueKind != JsonValueKind.Array)
            {
                throw MigrationBlocked("Workflow business model does not satisfy the latest contract.");
            }

            ValidateNodes(nodes, selectedNodeId.GetString()!);
            ValidateEdges(edges, nodes);
        }
        catch (JsonException exception)
        {
            throw MigrationBlocked($"Workflow business model JSON is invalid: {exception.Message}");
        }
    }

    private static void ValidateNodes(JsonElement nodes, string selectedNodeId)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        var hasStart = false;
        var hasEnd = false;
        foreach (var node in nodes.EnumerateArray())
        {
            if (node.ValueKind != JsonValueKind.Object ||
                !node.TryGetProperty("id", out var idElement) ||
                string.IsNullOrWhiteSpace(idElement.GetString()) ||
                !node.TryGetProperty("type", out var typeElement) ||
                string.IsNullOrWhiteSpace(typeElement.GetString()) ||
                !node.TryGetProperty("label", out var labelElement) ||
                labelElement.ValueKind != JsonValueKind.String ||
                !node.TryGetProperty("position", out var positionElement) ||
                positionElement.ValueKind != JsonValueKind.Object)
            {
                throw MigrationBlocked("Workflow business model contains a node without a stable id or type.");
            }

            var id = idElement.GetString()!;
            if (!identifiers.Add(id))
            {
                throw MigrationBlocked($"Workflow business model contains duplicate node id '{id}'.");
            }

            hasStart |= string.Equals(typeElement.GetString(), "start", StringComparison.Ordinal);
            hasEnd |= string.Equals(typeElement.GetString(), "end", StringComparison.Ordinal);
        }

        if (!hasStart || !hasEnd)
        {
            throw MigrationBlocked("Workflow business model requires one start node and one end node.");
        }

        if (!identifiers.Contains(selectedNodeId))
        {
            throw MigrationBlocked("Workflow business model selectedNodeId does not identify a node.");
        }
    }

    private static void ValidateEdges(JsonElement edges, JsonElement nodes)
    {
        var nodeIds = nodes.EnumerateArray()
            .Select(node => node.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var edge in edges.EnumerateArray())
        {
            if (edge.ValueKind != JsonValueKind.Object ||
                !edge.TryGetProperty("id", out var idElement) ||
                string.IsNullOrWhiteSpace(idElement.GetString()) ||
                !edge.TryGetProperty("source", out var sourceElement) ||
                !edge.TryGetProperty("target", out var targetElement) ||
                !nodeIds.Contains(sourceElement.GetString() ?? string.Empty) ||
                !nodeIds.Contains(targetElement.GetString() ?? string.Empty))
            {
                throw MigrationBlocked("Workflow business model contains an edge with an unknown endpoint.");
            }
        }
    }

    private static bool HasString(JsonElement value, string propertyName, string expected) =>
        value.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String &&
        string.Equals(property.GetString(), expected, StringComparison.Ordinal);

    private static ValidationException MigrationBlocked(string message) =>
        new($"{message} MigrationBlocked.", ErrorCodes.ParameterInvalid);
}
