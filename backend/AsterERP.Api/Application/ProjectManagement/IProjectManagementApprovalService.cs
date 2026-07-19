using AsterERP.Contracts.ProjectManagement;
using AsterERP.Contracts.Workflows;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementApprovalService
{
    Task<ProjectManagementApprovalStateResponse> GetAsync(string entityType, string entityId, string? idempotencyKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTimelineItemResponse>> GetHistoryAsync(string entityType, string entityId, string? idempotencyKey, CancellationToken cancellationToken = default);

    Task<ProjectManagementApprovalStateResponse> CompleteTaskAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task<ProjectManagementApprovalStateResponse> RejectTaskAsync(string taskId, WorkflowTaskActionRequest request, CancellationToken cancellationToken = default);

    Task<ProjectManagementApprovalStateResponse> WithdrawAsync(string entityType, string entityId, string? idempotencyKey, string? reason, CancellationToken cancellationToken = default);
}
