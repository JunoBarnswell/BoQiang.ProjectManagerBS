using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/data-space-exports")]
[Permission(PermissionCodes.ProjectManagementDataSpaceExport)]
public sealed class ProjectManagementDataSpaceExportsController(IProjectManagementDataSpaceExportService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken) => ApiOk(await service.ListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> StartAsync([FromBody] ProjectManagementDataSpaceExportRequest request, CancellationToken cancellationToken) => ApiOk(await service.StartAsync(request, cancellationToken));

    [HttpGet("{id}/download")]
    public async Task<IActionResult> DownloadAsync(string id, CancellationToken cancellationToken)
    {
        var result = await service.DownloadAsync(id, cancellationToken);
        return File(result.Stream, result.ContentType, result.FileName, enableRangeProcessing: false);
    }
}
