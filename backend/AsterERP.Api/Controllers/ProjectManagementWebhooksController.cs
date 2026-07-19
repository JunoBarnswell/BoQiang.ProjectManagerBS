using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[ApiController]
[Route("api/project-management/webhooks")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementWebhooksController(IProjectManagementWebhookService service) : BaseApiController
{
    [HttpGet("subscriptions")] public async Task<IActionResult> GetAsync([FromQuery] string projectId, CancellationToken ct) => ApiOk(await service.GetSubscriptionsAsync(projectId, ct));
    [HttpPut("subscriptions")] [Permission(PermissionCodes.ProjectManagementProjectEdit)] public async Task<IActionResult> SaveAsync([FromBody] ProjectManagementWebhookSubscriptionUpsertRequest request, CancellationToken ct) => ApiOk(await service.SaveSubscriptionAsync(request, ct));
    [HttpDelete("subscriptions/{id}")] [Permission(PermissionCodes.ProjectManagementProjectEdit)] public async Task<IActionResult> DeleteAsync(string id, CancellationToken ct) { await service.DeleteSubscriptionAsync(id, ct); return ApiOk<object?>(null); }
    [HttpGet("deliveries")] public async Task<IActionResult> DeliveriesAsync([FromQuery] string projectId, [FromQuery] GridQuery query, CancellationToken ct) => ApiOk(await service.GetDeliveriesAsync(projectId, query, ct));
    [HttpPost("deliveries/{eventId}/replay")] [Permission(PermissionCodes.ProjectManagementProjectEdit)] public async Task<IActionResult> ReplayAsync(string eventId, [FromBody] ProjectManagementWebhookReplayRequest request, CancellationToken ct) => ApiOk(await service.ReplayAsync(eventId, request, ct));
}
