using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/models")]
public sealed class ApplicationDataCenterModelsController(ApplicationDataModelService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterDataModelView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] ApplicationDataCenterObjectListQuery query, CancellationToken cancellationToken) => ApiOk(await service.GetPageAsync(query, cancellationToken));

    [HttpGet("{id}")]
    [Permission(PermissionCodes.AppDataCenterDataModelView)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterDataModelAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ApplicationDataCenterObjectUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AppDataCenterDataModelEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ApplicationDataCenterObjectUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AppDataCenterDataModelDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("{id}/enable")]
    [Permission(PermissionCodes.AppDataCenterDataModelEnable)]
    public async Task<IActionResult> EnableAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.EnableAsync(id, cancellationToken));

    [HttpPost("{id}/disable")]
    [Permission(PermissionCodes.AppDataCenterDataModelDisable)]
    public async Task<IActionResult> DisableAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DisableAsync(id, cancellationToken));

    [HttpPost("{id}/test")]
    [Permission(PermissionCodes.AppDataCenterDataModelTest)]
    public async Task<IActionResult> TestAsync(string id, [FromBody] ApplicationDataCenterActionRequest request, CancellationToken cancellationToken) => ApiOk(await service.TestAsync(id, request, cancellationToken));

    [HttpPost("{id}/preview")]
    [Permission(PermissionCodes.AppDataCenterDataModelPreview)]
    public async Task<IActionResult> PreviewAsync(string id, [FromBody] ApplicationDataCenterPreviewRequest request, CancellationToken cancellationToken) => ApiOk(await service.PreviewAsync(id, request, cancellationToken));

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.AppDataCenterDataModelPublish)]
    public async Task<IActionResult> PublishAsync(string id, [FromBody] ApplicationDataCenterPublishRequest request, CancellationToken cancellationToken) => ApiOk(await service.PublishAsync(id, request, cancellationToken));

    [HttpGet("{id}/references")]
    [Permission(PermissionCodes.AppDataCenterDataModelReference)]
    public async Task<IActionResult> GetReferencesAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetReferencesAsync(id, cancellationToken));
}
