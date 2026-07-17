namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowActivityResponse(
    string Id,
    string? ActivityId,
    string? ActivityName,
    string? ActivityType,
    string? ExecutionId,
    string? ProcessInstanceId,
    DateTime? StartTime,
    DateTime? EndTime,
    long? DurationInMillis);

public sealed record WorkflowHistoricProcessResponse(
    string Id,
    string? ProcessDefinitionId,
    string? BusinessKey,
    string? StartUserId,
    DateTime? StartTime,
    DateTime? EndTime,
    long? DurationInMillis,
    string? DeleteReason)
{
    public string? BusinessType { get; init; }

    public string? ProcessName { get; init; }

    public string? StarterUserName { get; init; }

    public string? Status { get; init; }
}

public sealed record WorkflowHistoricTaskResponse(
    string Id,
    string? Name,
    string? Assignee,
    string? Owner,
    string? ProcessInstanceId,
    string? TaskDefinitionKey,
    DateTime? StartTime,
    DateTime? EndTime,
    long? DurationInMillis,
    string? DeleteReason)
{
    public string? ProcessDefinitionId { get; init; }

    public string? BusinessType { get; init; }

    public string? BusinessKey { get; init; }

    public string? ProcessName { get; init; }

    public string? StarterUserName { get; init; }

    public string? AssigneeName { get; init; }

    public int CommentsCount { get; init; }

    public int AttachmentsCount { get; init; }
}

public sealed record WorkflowHistoricVariableResponse(
    string Id,
    string? Name,
    string? VariableType,
    object? Value,
    string? ProcessInstanceId,
    string? TaskId,
    DateTime? CreateTime,
    DateTime? LastUpdatedTime);
