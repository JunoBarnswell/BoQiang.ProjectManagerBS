using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed record ApplicationDataCenterSqlScriptExecutionResult(
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    ApplicationDataCenterPreviewResponse Preview,
    ApplicationDataCenterSqlScriptAuditSummaryResponse? Audit);
