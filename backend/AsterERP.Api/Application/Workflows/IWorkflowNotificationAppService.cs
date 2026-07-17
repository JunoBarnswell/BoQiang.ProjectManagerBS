using AsterERP.Contracts.Workflows;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowNotificationAppService
{
    Task<GridPageResult<WorkflowNotificationChannelResponse>> GetChannelsAsync(WorkflowNotificationQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowNotificationChannelResponse> SaveChannelAsync(WorkflowNotificationChannelUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteChannelAsync(string id, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowMessageTemplateResponse>> GetTemplatesAsync(WorkflowNotificationQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowMessageTemplateResponse> SaveTemplateAsync(WorkflowMessageTemplateUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(string id, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowNodeNotificationRuleResponse>> GetRulesAsync(WorkflowNotificationQuery query, CancellationToken cancellationToken = default);

    Task<WorkflowNodeNotificationRuleResponse> SaveRuleAsync(WorkflowNodeNotificationRuleUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteRuleAsync(string id, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowNotificationTaskResponse>> GetTasksAsync(WorkflowNotificationQuery query, CancellationToken cancellationToken = default);

    Task<GridPageResult<WorkflowNotificationLogResponse>> GetLogsAsync(WorkflowNotificationQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowNotificationTaskResponse>> GetInstanceNotificationsAsync(string processInstanceId, CancellationToken cancellationToken = default);

    Task<WorkflowNotificationPreviewResponse> PreviewReceiversAsync(WorkflowNotificationPreviewRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowTemplatePreviewResponse> PreviewTemplateAsync(WorkflowTemplatePreviewRequest request, CancellationToken cancellationToken = default);

    Task<WorkflowNotificationTaskResponse> TestSendAsync(WorkflowNotificationTestSendRequest request, CancellationToken cancellationToken = default);

    Task QueueAsync(WorkflowNotificationTriggerContext context, CancellationToken cancellationToken = default);

    Task<int> ProcessDueTasksAsync(int batchSize, CancellationToken cancellationToken = default);
}

public sealed record WorkflowNotificationTriggerContext(
    string TenantId,
    string AppCode,
    string? ModelId,
    string? ProcessDefinitionId,
    string? ProcessDefinitionKey,
    string? ProcessInstanceId,
    string? WorkflowTaskId,
    string? NodeId,
    string Trigger,
    string? StarterUserId,
    string? CurrentUserId,
    Dictionary<string, object?> Variables);
