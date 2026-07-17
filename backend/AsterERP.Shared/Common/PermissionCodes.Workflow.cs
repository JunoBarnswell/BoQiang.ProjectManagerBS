namespace AsterERP.Shared;

public static partial class PermissionCodes
{
    public const string WorkflowModelQuery = "workflow:model:query";
    public const string WorkflowModelAdd = "workflow:model:add";
    public const string WorkflowModelEdit = "workflow:model:edit";
    public const string WorkflowModelDelete = "workflow:model:delete";
    public const string WorkflowModelPublish = "workflow:model:publish";
    public const string WorkflowModelSuspend = "workflow:model:suspend";

    public const string WorkflowDeploymentQuery = "workflow:deployment:query";
    public const string WorkflowDeploymentResource = "workflow:deployment:resource";

    public const string WorkflowBindingQuery = "workflow:binding:query";
    public const string WorkflowBindingEdit = "workflow:binding:edit";
    public const string WorkflowBindingDelete = "workflow:binding:delete";

    public const string WorkflowFormQuery = "workflow:form:query";

    public const string WorkflowDraftQuery = "workflow:draft:query";
    public const string WorkflowDraftEdit = "workflow:draft:edit";
    public const string WorkflowDraftDelete = "workflow:draft:delete";
    public const string WorkflowDraftSubmit = "workflow:draft:submit";

    public const string WorkflowCategoryQuery = "workflow:category:query";
    public const string WorkflowCategoryEdit = "workflow:category:edit";
    public const string WorkflowCategoryDelete = "workflow:category:delete";

    public const string WorkflowInstanceQuery = "workflow:instance:query";
    public const string WorkflowInstanceStart = "workflow:instance:start";
    public const string WorkflowInstanceWithdraw = "workflow:instance:withdraw";
    public const string WorkflowInstanceTerminate = "workflow:instance:terminate";
    public const string WorkflowInstanceVariable = "workflow:instance:variable";

    public const string WorkflowTaskQuery = "workflow:task:query";
    public const string WorkflowTaskClaim = "workflow:task:claim";
    public const string WorkflowTaskApprove = "workflow:task:approve";
    public const string WorkflowTaskTransfer = "workflow:task:transfer";
    public const string WorkflowTaskDelegate = "workflow:task:delegate";
    public const string WorkflowTaskAttachment = "workflow:task:attachment";
    public const string WorkflowTaskComment = "workflow:task:comment";

    public const string WorkflowHistoryQuery = "workflow:history:query";

    public const string WorkflowReportQuery = "workflow:report:query";

    public const string WorkflowParticipantQuery = "workflow:participant:query";

    public const string WorkflowDelegationQuery = "workflow:delegation:query";
    public const string WorkflowDelegationEdit = "workflow:delegation:edit";
    public const string WorkflowDelegationDelete = "workflow:delegation:delete";

    public const string WorkflowCalendarQuery = "workflow:calendar:query";
    public const string WorkflowCalendarEdit = "workflow:calendar:edit";
    public const string WorkflowCalendarDelete = "workflow:calendar:delete";

    public const string WorkflowNotificationChannelQuery = "workflow:notification:channel:query";
    public const string WorkflowNotificationChannelEdit = "workflow:notification:channel:edit";
    public const string WorkflowNotificationChannelDelete = "workflow:notification:channel:delete";
    public const string WorkflowNotificationTemplateQuery = "workflow:notification:template:query";
    public const string WorkflowNotificationTemplateEdit = "workflow:notification:template:edit";
    public const string WorkflowNotificationTemplateDelete = "workflow:notification:template:delete";
    public const string WorkflowNotificationRuleQuery = "workflow:notification:rule:query";
    public const string WorkflowNotificationRuleEdit = "workflow:notification:rule:edit";
    public const string WorkflowNotificationRuleDelete = "workflow:notification:rule:delete";
    public const string WorkflowNotificationTaskQuery = "workflow:notification:task:query";
    public const string WorkflowNotificationTaskSend = "workflow:notification:task:send";
    public const string WorkflowNotificationLogQuery = "workflow:notification:log:query";
}
