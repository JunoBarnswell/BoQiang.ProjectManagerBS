namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleDraftSignalsResponse(
    int TotalRiskCount,
    bool HasPendingPublishRisk,
    IReadOnlyList<ApplicationConsoleDraftSignalResponse> Items);
