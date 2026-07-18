using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/notifications")]
[Permission(PermissionCodes.ProjectManagementNotificationView)]
public sealed class ProjectManagementNotificationsController(IProjectManagementNotificationService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementNotificationQuery query, CancellationToken cancellationToken = default) => ApiOk(await service.QueryAsync(query, cancellationToken));

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkReadAsync(string id, CancellationToken cancellationToken) { await service.MarkReadAsync(id, cancellationToken); return ApiOk(new { id }); }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllReadAsync(CancellationToken cancellationToken) { await service.MarkAllReadAsync(cancellationToken); return ApiOk(new { }); }

    [HttpPost("{id}/open")]
    public async Task<IActionResult> OpenAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.OpenAsync(id, cancellationToken));
}
