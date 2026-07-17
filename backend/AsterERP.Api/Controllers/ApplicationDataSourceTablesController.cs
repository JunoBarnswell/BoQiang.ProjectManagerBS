using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/data-sources/{dataSourceId}/tables")]
public sealed class ApplicationDataSourceTablesController(
    ApplicationDataSourceTableWorkbenchService service,
    ApplicationDataSourceTableRowService rowService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetTablesAsync(string dataSourceId, CancellationToken cancellationToken) =>
        ApiOk(await service.GetTablesAsync(dataSourceId, cancellationToken));

    [HttpGet("{tableName}")]
    [Permission(PermissionCodes.AppDataCenterDataSourceView)]
    public async Task<IActionResult> GetTableAsync(string dataSourceId, string tableName, CancellationToken cancellationToken) =>
        ApiOk(await service.GetTableAsync(dataSourceId, tableName, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> CreateTablePlanAsync(string dataSourceId, [FromBody] ApplicationDataSourceCreateTableRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateTablePlanAsync(dataSourceId, request, cancellationToken));

    [HttpPost("alter-plan")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> CreateAlterTablePlanAsync(string dataSourceId, [FromBody] ApplicationDataSourceAlterTableRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateAlterTablePlanAsync(dataSourceId, request, cancellationToken));

    [HttpPost("deploy")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> DeployTablePlanAsync(string dataSourceId, [FromBody] ApplicationDataSourceSchemaChangePlanRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.DeployTablePlanAsync(dataSourceId, request, cancellationToken));

    [HttpPost("alter-deploy")]
    [Permission(PermissionCodes.AppDataCenterDataSourceEdit)]
    public async Task<IActionResult> DeployAlterTablePlanAsync(string dataSourceId, [FromBody] ApplicationDataSourceAlterTablePlanRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.DeployAlterTablePlanAsync(dataSourceId, request, cancellationToken));

    [HttpPost("{tableName}/preview")]
    [Permission(PermissionCodes.AppDataCenterDataSourcePreview)]
    public async Task<IActionResult> PreviewTableAsync(string dataSourceId, string tableName, [FromBody] ApplicationDataCenterPreviewRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.PreviewTableAsync(dataSourceId, tableName, request.MaxRows, cancellationToken));

    [HttpPost("{tableName}/rows/query")]
    [Permission(PermissionCodes.AppDataCenterDataSourceDataQuery)]
    public async Task<IActionResult> QueryRowsAsync(string dataSourceId, string tableName, [FromBody] ApplicationDataSourceTableRowsQueryRequest request, CancellationToken cancellationToken) =>
        ApiOk(await rowService.QueryRowsAsync(dataSourceId, tableName, request, cancellationToken));

    [HttpPost("{tableName}/rows/export/stream")]
    [Permission(PermissionCodes.AppDataCenterDataSourceExport)]
    public async Task<IActionResult> ExportRowsStreamAsync(
        string dataSourceId,
        string tableName,
        [FromBody] ApplicationDataSourceTableRowsExportRequest request,
        CancellationToken cancellationToken)
    {
        var safeTableName = string.Concat(tableName.Select(character =>
            char.IsLetterOrDigit(character) || character is '-' or '_' or '.' ? character : '_'));
        Response.ContentType = "text/csv; charset=utf-8";
        Response.Headers.ContentDisposition = $"attachment; filename=\"{(string.IsNullOrWhiteSpace(safeTableName) ? "data" : safeTableName)}-export.csv\"";
        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        await rowService.StreamRowsExportAsync(dataSourceId, tableName, request, Response.Body, cancellationToken);
        return new EmptyResult();
    }

    [HttpPost("{tableName}/rows")]
    [Permission(PermissionCodes.AppDataCenterDataSourceDataEdit)]
    public async Task<IActionResult> InsertRowAsync(string dataSourceId, string tableName, [FromBody] ApplicationDataSourceTableRowUpsertRequest request, CancellationToken cancellationToken)
        => ApiOk(await rowService.InsertRowAsync(dataSourceId, tableName, request, cancellationToken));

    [HttpPut("{tableName}/rows")]
    [Permission(PermissionCodes.AppDataCenterDataSourceDataEdit)]
    public async Task<IActionResult> UpdateRowAsync(string dataSourceId, string tableName, [FromBody] ApplicationDataSourceTableRowUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await rowService.UpdateRowAsync(dataSourceId, tableName, request, cancellationToken);
        return result.Conflict ? StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(result, HttpContext.TraceIdentifier, "并发冲突")) : ApiOk(result);
    }

    [HttpDelete("{tableName}/rows")]
    [Permission(PermissionCodes.AppDataCenterDataSourceDataEdit)]
    public async Task<IActionResult> DeleteRowAsync(string dataSourceId, string tableName, [FromBody] ApplicationDataSourceTableRowDeleteRequest request, CancellationToken cancellationToken)
    {
        var result = await rowService.DeleteRowAsync(dataSourceId, tableName, request, cancellationToken);
        return result.Conflict ? StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Ok(result, HttpContext.TraceIdentifier, "并发冲突")) : ApiOk(result);
    }
}
