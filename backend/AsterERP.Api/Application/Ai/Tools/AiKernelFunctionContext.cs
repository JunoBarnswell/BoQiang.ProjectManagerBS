namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionContext
{
    public string TenantId { get; init; } = string.Empty;

    public string AppCode { get; init; } = string.Empty;

    public string OwnerUserId { get; init; } = string.Empty;

    public string? ConversationId { get; init; }

    public string? RunId { get; init; }

    public string? PlanId { get; init; }

    public string? PlanItemId { get; init; }

    public string TraceId { get; init; } = Guid.NewGuid().ToString("N");

    public string WorkMode { get; init; } = "Agent";

    public string? ModelConfigId { get; init; }

    public string? UserInstruction { get; init; }

    public string ArgumentsJson { get; init; } = "{}";

    public IReadOnlyDictionary<string, object?> Arguments { get; init; } =
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
}
