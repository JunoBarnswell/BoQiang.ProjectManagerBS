namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record AgentToolCall(string ToolCode, Dictionary<string, object?> Arguments);
