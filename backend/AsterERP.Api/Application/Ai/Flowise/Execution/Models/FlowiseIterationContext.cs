namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed record FlowiseIterationContext(int Index, string Value, bool IsFirst, bool IsLast, string ParentNodeId);
