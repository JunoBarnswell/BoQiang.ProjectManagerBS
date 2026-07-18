using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/project-management/reports")]
[Permission(PermissionCodes.ProjectManagementReportExport)]
public sealed class ProjectManagementReportsController(IProjectManagementReportService service) : BaseApiController
{
    [HttpGet("projects.csv")]
    public async Task<IActionResult> ExportCsvAsync([FromQuery] ProjectManagementReportQuery query, CancellationToken cancellationToken)
    {
        var file = await service.ExportCsvAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("projects.xlsx")]
    public async Task<IActionResult> ExportExcelAsync([FromQuery] ProjectManagementReportQuery query, CancellationToken cancellationToken)
    {
        var file = await service.ExportExcelAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
