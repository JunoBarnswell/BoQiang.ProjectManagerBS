using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/search")]
[Permission(PermissionCodes.ProjectManagementProjectView)]
public sealed class ProjectManagementSearchController(IProjectManagementSearchService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> SearchAsync([FromQuery] ProjectManagementSearchQuery query, CancellationToken cancellationToken) => ApiOk(await service.SearchAsync(query, cancellationToken));

    [HttpGet("index/status")]
    public async Task<IActionResult> GetIndexStatusAsync(CancellationToken cancellationToken) => ApiOk(await service.GetIndexStatusAsync(cancellationToken));

    [HttpPost("index/rebuild")]
    public async Task<IActionResult> RebuildIndexAsync([FromBody] ProjectManagementSearchIndexOperationRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.QueueIndexRebuildAsync(request, cancellationToken));

    [HttpPost("index/incremental")]
    public async Task<IActionResult> IncrementalIndexAsync([FromBody] ProjectManagementSearchIndexOperationRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.QueueIndexIncrementalAsync(request, cancellationToken));

    [HttpPost("index/recover")]
    public async Task<IActionResult> RecoverIndexAsync([FromBody] ProjectManagementSearchIndexOperationRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.QueueIndexRecoveryAsync(request, cancellationToken));
}
