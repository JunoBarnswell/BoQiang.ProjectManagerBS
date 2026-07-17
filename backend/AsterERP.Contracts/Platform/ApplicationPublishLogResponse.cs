namespace AsterERP.Contracts.Platform;

public sealed record ApplicationPublishLogResponse(
    string Id,
    string TaskId,
    string Level,
    string Stage,
    string Message,
    string TraceId,
    DateTime CreatedTime);
