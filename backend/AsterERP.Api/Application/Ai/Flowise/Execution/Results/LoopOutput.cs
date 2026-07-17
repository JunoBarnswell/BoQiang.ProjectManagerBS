namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record LoopOutput(string NodeId, int MaxLoopCount, string FallbackMessage, string Content);
