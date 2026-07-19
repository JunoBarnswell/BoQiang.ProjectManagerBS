using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementAutomationService
{
    Task<ProjectManagementAutomationRulesResponse> GetRulesAsync(string entityType, CancellationToken cancellationToken = default);

    Task<ProjectManagementAutomationRulesResponse> SaveRuleAsync(
        ProjectManagementAutomationRuleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementAutomationRuleRunResponse> RunRuleAsync(string entityType, string entityId, string ruleId, CancellationToken cancellationToken = default);

    Task<GridPageResult<ProjectManagementAutomationExecutionLogResponse>> GetExecutionLogsAsync(string entityType, string entityId, GridQuery query, CancellationToken cancellationToken = default);

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
        int automationDepth = 0,
        CancellationToken cancellationToken = default);
}
