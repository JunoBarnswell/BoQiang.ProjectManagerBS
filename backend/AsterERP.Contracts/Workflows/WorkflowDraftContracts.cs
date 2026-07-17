namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowRequestDraftResponse(
    string Id,
    string TenantId,
    string AppCode,
    string OwnerUserId,
    string? OwnerUserName,
    string FormResourceCode,
    string MenuCode,
    string BusinessType,
    string? BusinessKey,
    string Title,
    string DraftJson,
    string Status,
    DateTime LastSavedAt,
    DateTime? SubmittedAt,
    string? ProcessInstanceId,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowRequestDraftUpsertRequest(
    string? Id,
    string? TenantId,
    string? AppCode,
    string FormResourceCode,
    string MenuCode,
    string BusinessType,
    string? BusinessKey,
    string Title,
    string DraftJson);

public sealed record WorkflowRequestDraftSubmitRequest(
    string? Comment,
    Dictionary<string, object?>? Variables);
