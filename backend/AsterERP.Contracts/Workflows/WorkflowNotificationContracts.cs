using AsterERP.Shared;

namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowNotificationChannelResponse(
    string Id,
    string TenantId,
    string AppCode,
    string ChannelCode,
    string ChannelName,
    string ChannelType,
    bool IsEnabled,
    string? ConfigJson,
    string FailurePolicy,
    DateTime CreatedTime,
    DateTime? UpdatedTime);

public sealed record WorkflowNotificationChannelUpsertRequest(
    string? Id,
    string TenantId,
    string AppCode,
    string ChannelCode,
    string ChannelName,
    string ChannelType,
    bool IsEnabled,
    string? ConfigJson,
    string? FailurePolicy);

public sealed record WorkflowMessageTemplateResponse(
    string Id,
    string TenantId,
    string AppCode,
    string TemplateCode,
    string TemplateName,
    string ChannelType,
    string? SubjectTemplate,
    string BodyTemplate,
    string? VariablesJson,
    bool IsEnabled,
    DateTime CreatedTime,
    DateTime? UpdatedTime);

public sealed record WorkflowMessageTemplateUpsertRequest(
    string? Id,
    string TenantId,
    string AppCode,
    string TemplateCode,
    string TemplateName,
    string ChannelType,
    string? SubjectTemplate,
    string BodyTemplate,
    string? VariablesJson,
    bool IsEnabled);

public sealed record WorkflowNodeNotificationRuleResponse(
    string Id,
    string TenantId,
    string AppCode,
    string? ModelId,
    string? ProcessDefinitionId,
    string? ProcessDefinitionKey,
    string NodeId,
    string Trigger,
    string ReceiverType,
    string? ReceiverValue,
    IReadOnlyList<string> ChannelCodes,
    string TemplateCode,
    string? ConditionJson,
    string FailurePolicy,
    bool IsEnabled,
    DateTime CreatedTime,
    DateTime? UpdatedTime);

public sealed record WorkflowNodeNotificationRuleUpsertRequest(
    string? Id,
    string TenantId,
    string AppCode,
    string? ModelId,
    string? ProcessDefinitionId,
    string? ProcessDefinitionKey,
    string NodeId,
    string Trigger,
    string ReceiverType,
    string? ReceiverValue,
    IReadOnlyList<string>? ChannelCodes,
    string TemplateCode,
    string? ConditionJson,
    string? FailurePolicy,
    bool IsEnabled);

public sealed record WorkflowNotificationTaskResponse(
    string Id,
    string TenantId,
    string AppCode,
    string? RuleId,
    string? ProcessInstanceId,
    string? WorkflowTaskId,
    string? NodeId,
    string Trigger,
    string ChannelCode,
    string TemplateCode,
    string? ReceiverUserId,
    string? ReceiverAddress,
    string? Subject,
    string Content,
    string Status,
    int RetryCount,
    int MaxRetryCount,
    DateTime DueAt,
    DateTime? SentAt,
    string? LastError,
    DateTime CreatedTime);

public sealed record WorkflowNotificationLogResponse(
    string Id,
    string? NotificationTaskId,
    string? RuleId,
    string? ProcessInstanceId,
    string? WorkflowTaskId,
    string ChannelCode,
    string? ReceiverUserId,
    string EventName,
    string Result,
    string? Message,
    string? ErrorMessage,
    string? Provider,
    string? TraceId,
    DateTime CreatedTime);

public sealed record WorkflowNotificationPreviewRequest(
    string TenantId,
    string AppCode,
    string? ProcessInstanceId,
    string? WorkflowTaskId,
    string? NodeId,
    string Trigger,
    string ReceiverType,
    string? ReceiverValue);

public sealed record WorkflowNotificationPreviewResponse(
    IReadOnlyList<string> ReceiverUserIds,
    IReadOnlyList<string> ReceiverNames);

public sealed record WorkflowTemplatePreviewRequest(
    string TemplateCode,
    Dictionary<string, object?> Variables);

public sealed record WorkflowTemplatePreviewResponse(
    string? Subject,
    string Content);

public sealed record WorkflowNotificationTestSendRequest(
    string TenantId,
    string AppCode,
    string ChannelCode,
    string TemplateCode,
    string ReceiverUserId,
    Dictionary<string, object?> Variables);

public sealed record WorkflowNotificationQuery(
    int PageIndex = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Status = null,
    string? TenantId = null,
    string? AppCode = null,
    string? ProcessInstanceId = null,
    string? WorkflowTaskId = null)
{
    public GridQuery ToGridQuery() => new()
    {
        PageIndex = PageIndex,
        PageSize = PageSize,
        Keyword = Keyword,
        Status = Status,
        TenantId = TenantId,
        AppCode = AppCode
    };
}
