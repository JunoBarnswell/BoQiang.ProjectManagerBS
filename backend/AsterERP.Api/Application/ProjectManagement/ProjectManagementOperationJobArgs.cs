namespace AsterERP.Api.Application.ProjectManagement;

public sealed record ProjectManagementOperationJobArgs(
    string OperationId,
    string TenantId,
    string AppCode,
    string ActorUserId,
    string TraceId);
