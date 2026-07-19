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

    [HttpGet("tasks.csv")]
    public async Task<IActionResult> ExportTasksCsvAsync([FromQuery] ProjectManagementTaskQuery query, CancellationToken cancellationToken)
    {
        var file = await service.ExportTasksCsvAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("snapshots")]
    public async Task<IActionResult> StartSnapshotAsync([FromBody] ProjectManagementReportSnapshotRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.StartSnapshotAsync(request, cancellationToken));

    [HttpGet("snapshots/{operationId}/download")]
    public async Task<IActionResult> DownloadSnapshotAsync(string operationId, CancellationToken cancellationToken)
    {
        var file = await service.DownloadSnapshotAsync(operationId, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpPost("snapshots/{operationId}/retry")]
    public async Task<IActionResult> RetrySnapshotAsync(string operationId, CancellationToken cancellationToken) =>
        ApiOk(await service.RetrySnapshotAsync(operationId, cancellationToken));
}
