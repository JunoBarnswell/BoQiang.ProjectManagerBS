using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Contracts.Workflows;
using Microsoft.AspNetCore.Mvc;
using AsterERP.Shared;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/project-management/automation")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementAutomationController(IProjectManagementAutomationService service, IProjectManagementApprovalService approvalService) : BaseApiController
{
    [HttpGet("rules/{entityType}")]
    public async Task<IActionResult> GetRulesAsync(string entityType, CancellationToken cancellationToken) => ApiOk(await service.GetRulesAsync(entityType, cancellationToken));

    [HttpPut("rules")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> SaveRuleAsync([FromBody] ProjectManagementAutomationRuleUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.SaveRuleAsync(request, cancellationToken));

    [HttpPost("approvals/{entityType}/{entityId}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> StartApprovalAsync(string entityType, string entityId, [FromBody] ProjectManagementApprovalStartRequest request, CancellationToken cancellationToken) => ApiOk(await service.StartApprovalAsync(entityType, entityId, request, cancellationToken));

    [HttpGet("approvals/{entityType}/{entityId}")]
    public async Task<IActionResult> GetApprovalAsync(string entityType, string entityId, [FromQuery] string? idempotencyKey, CancellationToken cancellationToken) => ApiOk(await approvalService.GetAsync(entityType, entityId, idempotencyKey, cancellationToken));

    [HttpGet("approvals/{entityType}/{entityId}/history")]
    public async Task<IActionResult> GetApprovalHistoryAsync(string entityType, string entityId, [FromQuery] string? idempotencyKey, CancellationToken cancellationToken) => ApiOk(await approvalService.GetHistoryAsync(entityType, entityId, idempotencyKey, cancellationToken));

    [HttpPost("approvals/tasks/{taskId}/complete")]
    public async Task<IActionResult> CompleteApprovalTaskAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken) => ApiOk(await approvalService.CompleteTaskAsync(taskId, request, cancellationToken));

    [HttpPost("approvals/tasks/{taskId}/reject")]
    public async Task<IActionResult> RejectApprovalTaskAsync(string taskId, [FromBody] WorkflowTaskActionRequest request, CancellationToken cancellationToken) => ApiOk(await approvalService.RejectTaskAsync(taskId, request, cancellationToken));

    [HttpPost("approvals/{entityType}/{entityId}/withdraw")]
    public async Task<IActionResult> WithdrawApprovalAsync(string entityType, string entityId, [FromQuery] string? idempotencyKey, [FromBody] WorkflowTaskActionRequest? request, CancellationToken cancellationToken) => ApiOk(await approvalService.WithdrawAsync(entityType, entityId, idempotencyKey, request?.Comment, cancellationToken));

    [HttpPost("deliveries/{deliveryId}/replay")]
    public async Task<IActionResult> ReplayDeliveryAsync(string deliveryId, [FromBody] ProjectManagementAutomationReplayRequest request, CancellationToken cancellationToken) => ApiOk(await service.ReplayDeliveryAsync(deliveryId, request, cancellationToken));
}
