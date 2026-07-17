using System.Text;
using System.Text.Encodings.Web;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseNodeCatalogService(
    IFlowiseCanvasService canvasService,
    FlowisePermissionGuard permissionGuard) : IFlowiseNodeCatalogService
{
    public async Task<IReadOnlyList<FlowiseNodeDefinitionDto>> GetDefinitionsAsync(CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAnyView();
        var catalog = await canvasService.GetNodeCatalogAsync(cancellationToken);
        return catalog.Select(item => new FlowiseNodeDefinitionDto
        {
            BaseClasses = item.OutputAnchors.Select(anchor => anchor.Type).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Category = item.Category,
            Description = item.Description,
            Icon = item.Icon,
            InputAnchors = item.InputAnchors,
            InputParams = item.InputParams,
            Label = item.DisplayName,
            Name = item.NodeType,
            OutputAnchors = item.OutputAnchors,
            Tags = item.Tags,
            Type = item.NodeType,
            Version = item.Version
        }).ToList();
    }

    public Task<FlowiseNodeIcon> GetNodeIconAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Flowise node name is required");
        }

        var node = FlowiseCanvasNodeCatalog.Items.FirstOrDefault(item =>
            item.NodeType.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) ||
            item.DisplayName.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (node is null)
        {
            throw new ValidationException($"Flowise node {name} not found");
        }

        var color = ResolveNodeColor(node.Category);
        var label = ResolveNodeIconLabel(node.NodeType, node.DisplayName);
        var title = HtmlEncoder.Default.Encode(node.DisplayName);
        var encodedLabel = HtmlEncoder.Default.Encode(label);
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32" role="img" aria-label="{{title}}">
              <rect width="32" height="32" rx="8" fill="{{color}}"/>
              <text x="16" y="20.5" text-anchor="middle" font-family="Inter, Arial, sans-serif" font-size="10" font-weight="700" fill="#ffffff">{{encodedLabel}}</text>
            </svg>
            """;
        return Task.FromResult(new FlowiseNodeIcon(Encoding.UTF8.GetBytes(svg), "image/svg+xml", $"{node.NodeType}.svg"));
    }

    private static string ResolveNodeIconLabel(string nodeType, string displayName)
    {
        var source = string.IsNullOrWhiteSpace(nodeType) ? displayName : nodeType;
        var letters = new string(source.Where(char.IsLetterOrDigit).Take(2).Select(char.ToUpperInvariant).ToArray());
        return string.IsNullOrWhiteSpace(letters) ? "AI" : letters;
    }

    private static string ResolveNodeColor(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "agent" => "#7c3aed",
            "agentflow v2" => "#0891b2",
            "ai" => "#2563eb",
            "control" => "#dc2626",
            "data" => "#0f766e",
            "integration" => "#ea580c",
            "knowledge" => "#16a34a",
            "result" => "#4f46e5",
            "security" => "#9333ea",
            _ => "#64748b"
        };
    }
}
