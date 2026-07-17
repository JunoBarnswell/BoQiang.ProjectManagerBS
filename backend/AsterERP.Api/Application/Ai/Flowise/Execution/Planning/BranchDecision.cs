namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record BranchDecision(int SelectedIndex, string? SourceHandle, string TargetNodeId);
