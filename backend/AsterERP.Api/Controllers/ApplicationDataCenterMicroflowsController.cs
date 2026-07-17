using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-data-center/microflows")]
public sealed class ApplicationDataCenterMicroflowsController(ApplicationMicroflowService service) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AppDataCenterMicroflowView)]
    public async Task<IActionResult> GetPageAsync([FromQuery] ApplicationDataCenterObjectListQuery query, CancellationToken cancellationToken) => ApiOk(await service.GetPageAsync(query, cancellationToken));

    [HttpGet("{id}")]
    [Permission(PermissionCodes.AppDataCenterMicroflowView)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    [Permission(PermissionCodes.AppDataCenterMicroflowAdd)]
    public async Task<IActionResult> CreateAsync([FromBody] ApplicationDataCenterObjectUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id}")]
    [Permission(PermissionCodes.AppDataCenterMicroflowEdit)]
    public async Task<IActionResult> UpdateAsync(string id, [FromBody] ApplicationDataCenterObjectUpsertRequest request, CancellationToken cancellationToken) => ApiOk(await service.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id}")]
    [Permission(PermissionCodes.AppDataCenterMicroflowDelete)]
    public async Task<IActionResult> DeleteAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.DeleteAsync(id, cancellationToken));

    [HttpPost("{id}/test")]
    [Permission(PermissionCodes.AppDataCenterMicroflowTest)]
    public async Task<IActionResult> TestAsync(string id, [FromBody] ApplicationDataCenterActionRequest request, CancellationToken cancellationToken) => ApiOk(await service.TestAsync(id, request, cancellationToken));

    [HttpGet("{id}/versions")]
    [Permission(PermissionCodes.AppDataCenterMicroflowView)]
    public async Task<IActionResult> GetVersionsAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.ListVersionsAsync(id, cancellationToken));

    [HttpPost("{id}/versions/restore")]
    [Permission(PermissionCodes.AppDataCenterMicroflowEdit)]
    public async Task<IActionResult> RestoreVersionAsync(string id, [FromBody] ApplicationMicroflowRestoreRevisionRequest request, CancellationToken cancellationToken) => ApiOk(await service.RestoreRevisionAsync(id, request, cancellationToken));

    [HttpPost("{id}/validate")]
    [Permission(PermissionCodes.AppDataCenterMicroflowTest)]
    public async Task<IActionResult> ValidateAsync(string id, [FromBody] ApplicationMicroflowValidateRequest request, CancellationToken cancellationToken) => ApiOk(await service.ValidateRevisionAsync(id, request, cancellationToken));

    [HttpPost("{id}/execute-test")]
    [Permission(PermissionCodes.AppDataCenterMicroflowTest)]
    public async Task<IActionResult> ExecuteTestAsync(string id, [FromBody] ApplicationMicroflowExecuteRequest? request, CancellationToken cancellationToken) => ApiOk(await service.ExecutePublishedAsync(id, request ?? new ApplicationMicroflowExecuteRequest(), cancellationToken));

    [HttpPost("{id}/preview")]
    [Permission(PermissionCodes.AppDataCenterMicroflowPreview)]
    public async Task<IActionResult> PreviewAsync(string id, [FromBody] ApplicationDataCenterPreviewRequest request, CancellationToken cancellationToken) => ApiOk(await service.PreviewAsync(id, request, cancellationToken));

    [HttpPost("{id}/preview-run")]
    [Permission(PermissionCodes.AppDataCenterMicroflowPreview)]
    public async Task<IActionResult> PreviewRunAsync(string id, [FromBody] ApplicationMicroflowPreviewRequest request, CancellationToken cancellationToken) => ApiOk(await service.PreviewRunAsync(id, request, cancellationToken));

    [HttpPost("{id}/sql-script/run")]
    [Permission(PermissionCodes.AppDataCenterMicroflowPreview)]
    public async Task<IActionResult> RunSqlScriptAsync(string id, [FromBody] ApplicationMicroflowSqlScriptRunRequest request, CancellationToken cancellationToken) => ApiOk(await service.RunSqlScriptAsync(id, request, cancellationToken));

    [HttpPost("{id}/publish")]
    [Permission(PermissionCodes.AppDataCenterMicroflowPublish)]
    public async Task<IActionResult> PublishAsync(string id, [FromBody] ApplicationMicroflowPublishRequest request, CancellationToken cancellationToken) => ApiOk(await service.PublishRevisionAsync(id, request, cancellationToken));

    [HttpGet("{id}/references")]
    [Permission(PermissionCodes.AppDataCenterMicroflowReference)]
    public async Task<IActionResult> GetReferencesAsync(string id, CancellationToken cancellationToken) => ApiOk(await service.GetReferencesAsync(id, cancellationToken));
}
