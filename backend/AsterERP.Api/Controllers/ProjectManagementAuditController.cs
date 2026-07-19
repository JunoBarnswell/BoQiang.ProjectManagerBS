using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/audit")]
[Permission(PermissionCodes.ProjectManagementAuditView)]
public sealed class ProjectManagementAuditController(IProjectManagementAuditService service) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> QueryAsync([FromQuery] ProjectManagementAuditQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryAsync(query, cancellationToken));

    [HttpGet("{id}")]
    public async Task<IActionResult> DetailAsync(string id, CancellationToken cancellationToken)
        => ApiOk(await service.GetDetailAsync(id, cancellationToken));

    [HttpGet("operations")]
    public async Task<IActionResult> OperationsAsync([FromQuery] ProjectManagementOperationQuery query, CancellationToken cancellationToken)
        => ApiOk(await service.QueryOperationsAsync(query, cancellationToken));

    [HttpGet("export")]
    [Permission(PermissionCodes.ProjectManagementAuditExport)]
    public async Task<IActionResult> ExportAsync([FromQuery] ProjectManagementAuditQuery query, CancellationToken cancellationToken)
    {
        var result = await service.ExportAsync(query, cancellationToken);
        return File(result.Content, "text/csv; charset=utf-8", result.FileName);
    }
}
