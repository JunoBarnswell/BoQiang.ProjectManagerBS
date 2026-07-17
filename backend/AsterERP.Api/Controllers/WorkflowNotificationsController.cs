using AsterERP.Api.Application.Workflows;
using AsterERP.Contracts.Workflows;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/workflows/notifications")]
public sealed class WorkflowNotificationsController(IWorkflowNotificationAppService notificationService) : BaseApiController
{
    [HttpGet("channels")]
    [Permission(PermissionCodes.WorkflowNotificationChannelQuery)]
    public async Task<IActionResult> GetChannelsAsync([FromQuery] WorkflowNotificationQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.GetChannelsAsync(query, cancellationToken));
    }

    [HttpPost("channels")]
    [Permission(PermissionCodes.WorkflowNotificationChannelEdit)]
    public async Task<IActionResult> SaveChannelAsync([FromBody] WorkflowNotificationChannelUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.SaveChannelAsync(request, cancellationToken));
    }

    [HttpDelete("channels/{id}")]
    [Permission(PermissionCodes.WorkflowNotificationChannelDelete)]
    public async Task<IActionResult> DeleteChannelAsync(string id, CancellationToken cancellationToken)
    {
        await notificationService.DeleteChannelAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("templates")]
    [Permission(PermissionCodes.WorkflowNotificationTemplateQuery)]
    public async Task<IActionResult> GetTemplatesAsync([FromQuery] WorkflowNotificationQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.GetTemplatesAsync(query, cancellationToken));
    }

    [HttpPost("templates")]
    [Permission(PermissionCodes.WorkflowNotificationTemplateEdit)]
    public async Task<IActionResult> SaveTemplateAsync([FromBody] WorkflowMessageTemplateUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.SaveTemplateAsync(request, cancellationToken));
    }

    [HttpDelete("templates/{id}")]
    [Permission(PermissionCodes.WorkflowNotificationTemplateDelete)]
    public async Task<IActionResult> DeleteTemplateAsync(string id, CancellationToken cancellationToken)
    {
        await notificationService.DeleteTemplateAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("rules")]
    [Permission(PermissionCodes.WorkflowNotificationRuleQuery)]
    public async Task<IActionResult> GetRulesAsync([FromQuery] WorkflowNotificationQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.GetRulesAsync(query, cancellationToken));
    }

    [HttpPost("rules")]
    [Permission(PermissionCodes.WorkflowNotificationRuleEdit)]
    public async Task<IActionResult> SaveRuleAsync([FromBody] WorkflowNodeNotificationRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.SaveRuleAsync(request, cancellationToken));
    }

    [HttpDelete("rules/{id}")]
    [Permission(PermissionCodes.WorkflowNotificationRuleDelete)]
    public async Task<IActionResult> DeleteRuleAsync(string id, CancellationToken cancellationToken)
    {
        await notificationService.DeleteRuleAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("tasks")]
    [Permission(PermissionCodes.WorkflowNotificationTaskQuery)]
    public async Task<IActionResult> GetTasksAsync([FromQuery] WorkflowNotificationQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.GetTasksAsync(query, cancellationToken));
    }

    [HttpGet("logs")]
    [Permission(PermissionCodes.WorkflowNotificationLogQuery)]
    public async Task<IActionResult> GetLogsAsync([FromQuery] WorkflowNotificationQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.GetLogsAsync(query, cancellationToken));
    }

    [HttpPost("preview-receivers")]
    [Permission(PermissionCodes.WorkflowNotificationRuleQuery)]
    public async Task<IActionResult> PreviewReceiversAsync([FromBody] WorkflowNotificationPreviewRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.PreviewReceiversAsync(request, cancellationToken));
    }

    [HttpPost("preview-template")]
    [Permission(PermissionCodes.WorkflowNotificationTemplateQuery)]
    public async Task<IActionResult> PreviewTemplateAsync([FromBody] WorkflowTemplatePreviewRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.PreviewTemplateAsync(request, cancellationToken));
    }

    [HttpPost("test-send")]
    [Permission(PermissionCodes.WorkflowNotificationTaskSend)]
    public async Task<IActionResult> TestSendAsync([FromBody] WorkflowNotificationTestSendRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await notificationService.TestSendAsync(request, cancellationToken));
    }
}
