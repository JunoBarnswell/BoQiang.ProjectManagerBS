namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationIntegrationTaskRunResponse(
    bool Success,
    string Result,
    int ReadCount,
    int SuccessCount,
    int FailedCount,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> FailedRows,
    string? ErrorMessage,
    string? RunId,
    bool IsDryRun,
    string SnapshotId,
    int SnapshotVersion);
