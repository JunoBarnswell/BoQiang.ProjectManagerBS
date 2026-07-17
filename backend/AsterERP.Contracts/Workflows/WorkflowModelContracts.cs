namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowModelListItemResponse(
    string Id,
    string ModelId,
    string ModelKey,
    string Name,
    string AppCode,
    string CategoryCode,
    int? Status,
    int? ExtendStatus,
    int? Version,
    string? ProcessDefinitionId,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowModelDetailResponse(
    string Id,
    string ModelId,
    string ModelKey,
    string Name,
    string AppCode,
    string CategoryCode,
    int? Status,
    int? ExtendStatus,
    int? Version,
    string? ProcessDefinitionId,
    string BpmnXml,
    string? ExtensionJson,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record WorkflowModelUpsertRequest(
    string? Id,
    string? ModelId,
    string ModelKey,
    string Name,
    string? AppCode,
    string? CategoryCode,
    int? ModelType,
    int? FormType,
    string? Remark);

public sealed record WorkflowModelXmlSaveRequest(
    string BpmnXml,
    string? ExtensionJson);

public sealed record WorkflowModelValidationResponse(
    bool IsValid,
    IReadOnlyList<string> Errors);

public sealed record WorkflowModelPublishResponse(
    string ModelId,
    string DeploymentId,
    string ProcessDefinitionId,
    int Version);

public sealed record WorkflowModelVersionResponse(
    string ProcessDefinitionId,
    string? DeploymentId,
    string? Key,
    string? Name,
    int Version,
    bool IsSuspended,
    string? TenantId);
