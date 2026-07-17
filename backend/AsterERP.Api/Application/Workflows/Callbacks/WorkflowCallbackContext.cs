using AsterERP.Api.Modules.Workflows;

namespace AsterERP.Api.Application.Workflows.Callbacks;

public sealed record WorkflowCallbackContext(
    WorkflowBusinessInstanceEntity Instance,
    string Trigger,
    string? NodeId,
    string? WorkflowTaskId,
    string? Action,
    string CurrentUserId,
    IReadOnlyDictionary<string, object?> Variables,
    DateTime OccurredAt);
