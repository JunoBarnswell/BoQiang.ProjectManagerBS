namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowBindingResponse(
    string Id,
    string TenantId,
    string AppCode,
    string MenuCode,
    string BusinessType,
    string ProcessDefinitionKey,
    string? ProcessDefinitionId,
    string? ModelId,
    string? ModelKey,
    string? FormResourceCode,
    string? PageCode,
    string? ModelCode,
    string? KeyField,
    string? DetailRoute,
    string? TitleTemplate,
    bool IsEnabled,
    string? StartFormJson,
    WorkflowCallbackConfigDto? CallbackConfig,
    string? Remark);

public sealed record WorkflowBindingUpsertRequest(
    string TenantId,
    string AppCode,
    string MenuCode,
    string BusinessType,
    string ProcessDefinitionKey,
    string? ProcessDefinitionId,
    string? ModelId,
    string? ModelKey,
    string? FormResourceCode,
    string? PageCode,
    string? ModelCode,
    string? KeyField,
    string? DetailRoute,
    string? TitleTemplate,
    bool IsEnabled,
    string? StartFormJson,
    WorkflowCallbackConfigDto? CallbackConfig,
    string? Remark);

public sealed record WorkflowCallbackConfigDto(
    IReadOnlyList<WorkflowCallbackRuleDto>? Rules,
    string Version = "latest");

public sealed record WorkflowCallbackRuleDto(
    string? RuleId,
    bool Enabled,
    string Trigger,
    string? NodeId,
    WorkflowCallbackTargetDto? Target,
    IReadOnlyList<WorkflowCallbackAssignmentDto>? Assignments,
    int SortOrder);

public sealed record WorkflowCallbackTargetDto(
    string? ModelCode,
    string? KeySource,
    string? KeyName);

public sealed record WorkflowCallbackAssignmentDto(
    string FieldCode,
    string ValueSource,
    object? Value,
    string? ValueName);
