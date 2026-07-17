using System.Text.Json;

namespace AsterERP.Contracts.ApplicationDevelopmentCenter;

public sealed record ApplicationDesignerEditorSessionDto(
    string SessionId,
    string DocumentId,
    string? PrimaryNodeId,
    IReadOnlyList<string> SelectedNodeIds,
    string? AnchorNodeId,
    JsonElement Viewport,
    JsonElement PanelState,
    string? TransactionId);
