using AsterERP.Api.Application.System.Printing;
using AsterERP.Contracts.System.Printing;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/system/print-center")]
public sealed class SystemPrintCenterController(
    PrintTargetCatalog targetCatalog,
    SystemPrintTemplateService templateService,
    SystemPrintCustomElementService customElementService,
    SystemPrintRuntimeService runtimeService) : BaseApiController
{
    [HttpGet("targets")]
    [Permission(PermissionCodes.SystemPrintQuery)]
    public async Task<IActionResult> GetTargetsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await targetCatalog.GetTargetsAsync(cancellationToken));
    }

    [HttpGet("targets/{menuCode}")]
    [Permission(PermissionCodes.SystemPrintQuery)]
    public async Task<IActionResult> GetTargetDetailAsync(string menuCode, [FromQuery] string? scene, CancellationToken cancellationToken)
    {
        return ApiOk(await targetCatalog.GetTargetDetailAsync(menuCode, scene, cancellationToken));
    }

    [HttpGet("templates")]
    [Permission(PermissionCodes.SystemPrintQuery)]
    public async Task<IActionResult> GetTemplatesAsync(
        [FromQuery] string? keyword,
        [FromQuery] string? menuCode,
        [FromQuery] string? scene,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.GetPageAsync(keyword, menuCode, scene, status, cancellationToken));
    }

    [HttpGet("templates/options")]
    [Permission(PermissionCodes.SystemPrintUse)]
    public async Task<IActionResult> GetTemplateOptionsAsync(
        [FromQuery] string menuCode,
        [FromQuery] string scene,
        CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.GetOptionsAsync(menuCode, scene, cancellationToken));
    }

    [HttpGet("templates/{id}")]
    [Permission(PermissionCodes.SystemPrintQuery)]
    public async Task<IActionResult> GetTemplateAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("templates")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> UpsertTemplateAsync(
        [FromBody] PrintTemplateUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.UpsertAsync(request, cancellationToken: cancellationToken));
    }

    [HttpPut("templates/{id}")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> UpdateTemplateAsync(
        string id,
        [FromBody] PrintTemplateUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.UpsertAsync(request with { Id = id }, cancellationToken: cancellationToken));
    }

    [HttpDelete("templates/{id}")]
    [Permission(PermissionCodes.SystemPrintDelete)]
    public async Task<IActionResult> DeleteTemplateAsync(string id, CancellationToken cancellationToken)
    {
        await templateService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpPost("templates/{id}/publish")]
    [Permission(PermissionCodes.SystemPrintPublish)]
    public async Task<IActionResult> PublishTemplateAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.PublishAsync(id, cancellationToken));
    }

    [HttpPost("templates/{id}/set-default")]
    [Permission(PermissionCodes.SystemPrintDefault)]
    public async Task<IActionResult> SetDefaultTemplateAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.SetDefaultAsync(id, cancellationToken));
    }

    [HttpPost("resolve-runtime")]
    [Permission(PermissionCodes.SystemPrintUse)]
    public async Task<IActionResult> ResolveRuntimeAsync(
        [FromBody] PrintTemplateResolveRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await runtimeService.ResolveRuntimeAsync(request, cancellationToken));
    }

    [HttpGet("designer/templates")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> GetDesignerTemplatesAsync(
        [FromQuery] string? menuCode,
        [FromQuery] string? scene,
        CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.GetPageAsync(null, menuCode, scene, null, cancellationToken));
    }

    [HttpGet("designer/templates/{id}")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> GetDesignerTemplateAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("designer/templates")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> UpsertDesignerTemplateAsync(
        [FromQuery] string? menuCode,
        [FromQuery] string? scene,
        [FromBody] PrintTemplateUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await templateService.UpsertAsync(request, menuCode, scene, cancellationToken));
    }

    [HttpDelete("designer/templates/{id}")]
    [Permission(PermissionCodes.SystemPrintDelete)]
    public async Task<IActionResult> DeleteDesignerTemplateAsync(string id, CancellationToken cancellationToken)
    {
        await templateService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }

    [HttpGet("designer/custom-elements")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> GetDesignerCustomElementsAsync(CancellationToken cancellationToken)
    {
        return ApiOk(await customElementService.GetListAsync(cancellationToken));
    }

    [HttpGet("designer/custom-elements/{id}")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> GetDesignerCustomElementAsync(string id, CancellationToken cancellationToken)
    {
        return ApiOk(await customElementService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost("designer/custom-elements")]
    [Permission(PermissionCodes.SystemPrintEdit)]
    public async Task<IActionResult> UpsertDesignerCustomElementAsync(
        [FromBody] PrintCustomElementUpsertRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await customElementService.UpsertAsync(request, cancellationToken));
    }

    [HttpDelete("designer/custom-elements/{id}")]
    [Permission(PermissionCodes.SystemPrintDelete)]
    public async Task<IActionResult> DeleteDesignerCustomElementAsync(string id, CancellationToken cancellationToken)
    {
        await customElementService.DeleteAsync(id, cancellationToken);
        return ApiOk(true);
    }
}
