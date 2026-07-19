using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementAutomationService
{
    Task<ProjectManagementAutomationRulesResponse> GetRulesAsync(string entityType, CancellationToken cancellationToken = default);

    Task<ProjectManagementAutomationRulesResponse> SaveRuleAsync(
        ProjectManagementAutomationRuleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementApprovalResponse> StartApprovalAsync(
        string entityType,
        string entityId,
        ProjectManagementApprovalStartRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementAutomationDeliveryResponse> ReplayDeliveryAsync(
        string deliveryId,
        ProjectManagementAutomationReplayRequest request,
        CancellationToken cancellationToken = default);

    Task HandleEntityChangedAsync(
        string entityType,
        string entityId,
        string projectId,
        string? status,
        string? assigneeUserId,
        string? milestoneId,
        DateTime? dueDate,
        long versionNo,
        string eventType,
        string traceId,
        CancellationToken cancellationToken = default);
}
