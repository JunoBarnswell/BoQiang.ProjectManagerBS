namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowDelegationRuleResponse(
    string Id,
    string TenantId,
    string AppCode,
    string OwnerUserId,
    string? OwnerUserName,
    string DelegateUserId,
    string? DelegateUserName,
    string ScopeType,
    string? ProcessDefinitionKey,
    DateTime StartAt,
    DateTime EndAt,
    bool IsEnabled,
    string? Reason,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowDelegationRuleUpsertRequest(
    string? Id,
    string? TenantId,
    string? AppCode,
    string DelegateUserId,
    string? ScopeType,
    string? ProcessDefinitionKey,
    DateTime StartAt,
    DateTime EndAt,
    bool? IsEnabled,
    string? Reason);
