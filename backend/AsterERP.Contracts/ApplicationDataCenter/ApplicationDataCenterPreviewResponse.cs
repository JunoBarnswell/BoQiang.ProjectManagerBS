namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterPreviewResponse(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> Fields,
    string? Message,
    IReadOnlyList<ApplicationDataCenterPreviewDatasetResponse>? Datasets = null,
    ApplicationDataCenterPreviewPageResponse? Page = null,
    ApplicationDataCenterSqlScriptAuditSummaryResponse? Audit = null);
