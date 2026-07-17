using AsterERP.Api.Application.System.QueryViews;
using AsterERP.Shared;
using AsterERP.Contracts.System.QueryViews;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/query-views")]
public sealed class SystemQueryViewRuntimeController(
    IQueryViewRuntimeService runtimeService,
    IQueryViewExportService exportService) : BaseApiController
{
    [HttpGet("{viewCode}/definition")]
    [Permission(PermissionCodes.SystemQueryViewQuery)]
    public async Task<IActionResult> GetDefinitionAsync(string viewCode, CancellationToken cancellationToken)
    {
        return ApiOk(await runtimeService.GetDefinitionAsync(viewCode, cancellationToken));
    }

    [HttpPost("{viewCode}/query")]
    [Permission(PermissionCodes.SystemQueryViewQuery)]
    public async Task<IActionResult> QueryAsync(string viewCode, [FromBody] QueryViewQueryRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await runtimeService.QueryAsync(viewCode, request, cancellationToken));
    }

    [HttpPost("{viewCode}/export")]
    [Permission(PermissionCodes.SystemQueryViewExport)]
    public async Task<IActionResult> ExportAsync(string viewCode, [FromBody] QueryViewExportRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await exportService.ExportAsync(viewCode, request, cancellationToken));
    }

    [HttpGet("export-tasks")]
    [Permission(PermissionCodes.SystemQueryViewTask)]
    public async Task<IActionResult> GetExportTasksAsync([FromQuery] string? viewCode, CancellationToken cancellationToken)
    {
        return ApiOk(await exportService.GetTasksAsync(viewCode, cancellationToken));
    }
}
