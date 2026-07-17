namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationConnectionCheckRunResponse(
    string Id,
    string TemplateCode,
    string Result,
    DateTime StartedAt,
    DateTime? FinishedAt,
    long DurationMs,
    string? ErrorMessage,
    string? ResultJson);
