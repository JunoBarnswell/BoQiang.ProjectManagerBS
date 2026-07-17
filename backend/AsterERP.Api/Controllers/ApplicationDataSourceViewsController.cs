using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/data-sources/{dataSourceId}/views")]
public sealed class ApplicationDataSourceViewsController(ApplicationDataSourceViewWorkbenchService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetView)]
    public async Task<IActionResult> GetListAsync(string dataSourceId, CancellationToken cancellationToken) =>
        ApiOk(await service.GetListAsync(dataSourceId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetAdd)]
    public async Task<IActionResult> CreateAsync(string dataSourceId, [FromBody] ApplicationDataSourceViewUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateAsync(dataSourceId, request, cancellationToken));

    [HttpPut("{viewId}")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetEdit)]
    public async Task<IActionResult> UpdateAsync(string dataSourceId, string viewId, [FromBody] ApplicationDataSourceViewUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateAsync(dataSourceId, viewId, request, cancellationToken));

    [HttpDelete("{viewId}")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetDelete)]
    public async Task<IActionResult> DeleteAsync(string dataSourceId, string viewId, CancellationToken cancellationToken) =>
        ApiOk(await service.DeleteAsync(dataSourceId, viewId, cancellationToken));

    [HttpPost("preview-sql")]
    [Permission(PermissionCodes.AppDataCenterQueryDatasetPreview)]
    public async Task<IActionResult> PreviewSqlAsync(string dataSourceId, [FromBody] ApplicationDataSourceSqlPreviewRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.PreviewSqlAsync(dataSourceId, request, cancellationToken));
}
