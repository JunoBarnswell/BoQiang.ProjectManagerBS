namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleSummaryResponse(
    ApplicationConsoleApplicationResponse Application,
    ApplicationDatabaseBindingStatusResponse DatabaseBinding,
    IReadOnlyList<ApplicationConsoleMetricResponse> Metrics,
    ApplicationConsoleCapabilityCountsResponse CapabilityCounts,
    IReadOnlyList<ApplicationConsoleRecentItemResponse> RecentPublishes,
    IReadOnlyList<ApplicationConsoleRecentItemResponse> RecentAudits,
    IReadOnlyList<ApplicationConsoleEntryTreeGroupResponse> EntryTree,
    IReadOnlyList<ApplicationConsoleDevelopmentShortcutResponse> DevelopmentShortcuts,
    IReadOnlyList<ApplicationConsoleRecentDevelopmentItemResponse> RecentDevelopmentItems,
    ApplicationConsoleVersionContextResponse VersionContext,
    ApplicationConsoleDraftSignalsResponse DraftSignals);
