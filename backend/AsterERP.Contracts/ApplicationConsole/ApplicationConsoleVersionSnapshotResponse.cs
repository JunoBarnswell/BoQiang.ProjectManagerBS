namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleVersionSnapshotResponse(
    string Id,
    string VersionName,
    string VersionCode,
    string Status,
    DateTime UpdatedTime);
