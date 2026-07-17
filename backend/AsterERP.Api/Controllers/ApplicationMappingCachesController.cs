using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/data-sources/{dataSourceId}/mapping-caches")]
public sealed class ApplicationMappingCachesController(ApplicationMappingCacheWorkbenchService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterDictionaryCodeView)]
    public async Task<IActionResult> GetListAsync(string dataSourceId, CancellationToken cancellationToken) =>
        ApiOk(await service.GetListAsync(dataSourceId, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterDictionaryCodeAdd)]
    public async Task<IActionResult> CreateAsync(string dataSourceId, [FromBody] ApplicationMappingCacheUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.CreateAsync(dataSourceId, request, cancellationToken));

    [HttpPut("{cacheId}")]
    [Permission(PermissionCodes.AppDataCenterDictionaryCodeEdit)]
    public async Task<IActionResult> UpdateAsync(string dataSourceId, string cacheId, [FromBody] ApplicationMappingCacheUpsertRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateAsync(dataSourceId, cacheId, request, cancellationToken));

    [HttpDelete("{cacheId}")]
    [Permission(PermissionCodes.AppDataCenterDictionaryCodeDelete)]
    public async Task<IActionResult> DeleteAsync(string dataSourceId, string cacheId, CancellationToken cancellationToken) =>
        ApiOk(await service.DeleteAsync(dataSourceId, cacheId, cancellationToken));

    [HttpPost("{cacheId}/test")]
    [Permission(PermissionCodes.AppDataCenterDictionaryCodeTest)]
    public async Task<IActionResult> TestAsync(string dataSourceId, string cacheId, [FromBody] ApplicationMappingCacheTestRequest request, CancellationToken cancellationToken) =>
        ApiOk(await service.TestAsync(dataSourceId, cacheId, request, cancellationToken));

    [HttpPost("{cacheId}/refresh")]
    [Permission(PermissionCodes.AppDataCenterDictionaryCodePublish)]
    public async Task<IActionResult> RefreshAsync(string dataSourceId, string cacheId, [FromBody] ApplicationMappingCacheTestRequest? request, CancellationToken cancellationToken) =>
        ApiOk(await service.RefreshAsync(dataSourceId, cacheId, request, cancellationToken));
}
