namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleVersionContextResponse(
    int DraftVersionCount,
    int PublishedVersionCount,
    ApplicationConsoleVersionSnapshotResponse? LatestDraftVersion,
    ApplicationConsoleVersionSnapshotResponse? LatestPublishedVersion,
    DateTime? LatestPublishTime,
    string Summary);
