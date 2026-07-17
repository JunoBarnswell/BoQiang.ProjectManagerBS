namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowFormFieldResponse(
    string FieldCode,
    string FieldName,
    string DataType,
    string Binding,
    bool Visible,
    bool Queryable,
    bool Sortable,
    bool Writable,
    string? Renderer,
    string? DictType,
    int Order);

public sealed record WorkflowFormResourceResponse(
    string ResourceCode,
    string ResourceName,
    string MenuCode,
    string BusinessType,
    string? RoutePath,
    string PageCode,
    string ModelCode,
    string KeyField,
    string? PermissionCode,
    IReadOnlyList<WorkflowFormFieldResponse> Fields);

public sealed record WorkflowBindingStatusRequest(
    string? PageCode,
    string? ModelCode,
    IReadOnlyList<string>? BusinessKeys);

public sealed record WorkflowBusinessApprovalStatusResponse(
    string BusinessKey,
    bool HasHistory,
    string? LatestStatus,
    string? ProcessInstanceId,
    string? ProcessDefinitionKey,
    DateTime? StartedAt,
    DateTime? FinishedAt);

public sealed record WorkflowBindingStatusResponse(
    string? PageCode,
    string? ModelCode,
    WorkflowBindingResponse? Binding,
    IReadOnlyList<WorkflowBusinessApprovalStatusResponse> Items);
