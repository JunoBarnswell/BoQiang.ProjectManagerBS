namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowDeploymentListItemResponse(
    string Id,
    string? Name,
    string? Category,
    string? Key,
    string? TenantId,
    DateTime? DeployTime,
    IReadOnlyList<string> Resources);

public sealed record WorkflowProcessDefinitionResponse(
    string Id,
    string? Key,
    string? Name,
    string? DeploymentId,
    int Version,
    string? Category,
    string? Description,
    bool IsSuspended,
    string? TenantId);

public sealed record WorkflowDeploymentResourceResponse(
    string DeploymentId,
    string ResourceName,
    string ContentType,
    string Content);
