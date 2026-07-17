namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationConnectionDiagnosticStageResponse(
    string Code,
    string Status,
    long DurationMs,
    string Message,
    string? RepairSuggestion,
    string? DetailJson);
