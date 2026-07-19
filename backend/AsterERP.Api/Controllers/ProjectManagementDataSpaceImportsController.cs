using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/data-space-imports")]
[Permission(PermissionCodes.ProjectManagementDataSpaceImport)]
public sealed class ProjectManagementDataSpaceImportsController(IProjectManagementDataSpaceImportService service) : BaseApiController
{
    [HttpPost]
    public async Task<IActionResult> StartAsync([FromBody] ProjectManagementDataSpaceImportRequest request, CancellationToken cancellationToken) => ApiOk(await service.StartAsync(request, cancellationToken));
}
