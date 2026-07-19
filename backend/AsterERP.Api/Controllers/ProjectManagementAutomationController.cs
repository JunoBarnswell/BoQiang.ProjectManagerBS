using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.Mvc;
using AsterERP.Shared;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/project-management/automation")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementAutomationController(IProjectManagementAutomationService service) : BaseApiController
{
    [HttpGet("rules/{entityType}")]
    public async Task<IActionResult> GetRulesAsync(string entityType, CancellationToken cancellationToken) => ApiOk(await service.GetRulesAsync(entityType, cancellationToken));

    [HttpPut("rules")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> SaveRuleAsync([FromBody] ProjectManagementAutomationRuleUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.SaveRuleAsync(request, cancellationToken));

    [HttpPost("approvals/{entityType}/{entityId}")]
    [Permission(PermissionCodes.ProjectManagementProjectEdit)]
    public async Task<IActionResult> StartApprovalAsync(string entityType, string entityId, [FromBody] ProjectManagementApprovalStartRequest request, CancellationToken cancellationToken) => ApiOk(await service.StartApprovalAsync(entityType, entityId, request, cancellationToken));

    [HttpPost("deliveries/{deliveryId}/replay")]
    public async Task<IActionResult> ReplayDeliveryAsync(string deliveryId, [FromBody] ProjectManagementAutomationReplayRequest request, CancellationToken cancellationToken) => ApiOk(await service.ReplayDeliveryAsync(deliveryId, request, cancellationToken));
}
