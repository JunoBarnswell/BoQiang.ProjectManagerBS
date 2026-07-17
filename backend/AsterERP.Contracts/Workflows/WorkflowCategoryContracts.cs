namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowCategoryResponse(
    string Id,
    string TenantId,
    string AppCode,
    string CategoryCode,
    string CategoryName,
    string? ParentCode,
    int SortOrder,
    bool IsEnabled,
    string? Remark,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowCategoryUpsertRequest(
    string? Id,
    string? TenantId,
    string? AppCode,
    string CategoryCode,
    string CategoryName,
    string? ParentCode,
    int? SortOrder,
    bool? IsEnabled,
    string? Remark);
