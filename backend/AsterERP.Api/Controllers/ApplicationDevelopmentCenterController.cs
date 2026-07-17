using AsterERP.Api.Application.ApplicationDevelopmentCenter;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ApplicationDevelopmentCenter;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/application-development-center")]
public sealed class ApplicationDevelopmentCenterController(
    ApplicationDevelopmentCenterService service,
    IAuthSessionService authSessionService,
    ApplicationMonitoringEventService monitoringEventService) : BaseApiController
{
    [HttpPost("monitoring/events")]
    [Permission(PermissionCodes.AppDevelopmentCenterMonitoringWrite)]
    public async Task<IActionResult> AcceptMonitoringEventAsync(
        [FromBody] ApplicationMonitoringEventRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await monitoringEventService.AcceptAsync(request, cancellationToken));
    [HttpGet("overview")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetOverviewAsync(CancellationToken cancellationToken) =>
        ApiOk(await service.GetOverviewAsync(cancellationToken));

    [HttpGet("app-config")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetAppConfigAsync(CancellationToken cancellationToken) =>
        ApiOk(await service.GetAppConfigAsync(cancellationToken));

    [HttpPut("app-config")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> SaveAppConfigAsync(
        [FromBody] ApplicationDevelopmentAppConfigRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.SaveAppConfigAsync(request, cancellationToken));

    [HttpGet("versions")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetVersionsAsync(CancellationToken cancellationToken) =>
        ApiOk(await service.GetVersionsAsync(cancellationToken));

    [HttpGet("workspace")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetWorkspaceAsync(
        [FromQuery] string? versionId,
        CancellationToken cancellationToken) =>
        ApiOk(await service.GetWorkspaceAsync(versionId, cancellationToken));

    [HttpPost("versions")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> CreateVersionAsync(
        [FromBody] ApplicationDevelopmentVersionUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.CreateVersionAsync(request, cancellationToken));

    [HttpPut("versions/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> UpdateVersionAsync(
        string id,
        [FromBody] ApplicationDevelopmentVersionUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateVersionAsync(id, request, cancellationToken));

    [HttpGet("modules")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetModulesAsync(
        [FromQuery] string versionId,
        CancellationToken cancellationToken) =>
        ApiOk(await service.GetModulesAsync(versionId, cancellationToken));

    [HttpPost("modules")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> CreateModuleAsync(
        [FromBody] ApplicationDevelopmentModuleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.CreateModuleAsync(request, cancellationToken);
        await authSessionService.InvalidateSessionCacheAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return ApiOk(response);
    }

    [HttpPut("modules/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> UpdateModuleAsync(
        string id,
        [FromBody] ApplicationDevelopmentModuleUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var response = await service.UpdateModuleAsync(id, request, cancellationToken);
        await authSessionService.InvalidateSessionCacheAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return ApiOk(response);
    }

    [HttpDelete("modules/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> DeleteModuleAsync(string id, CancellationToken cancellationToken)
    {
        var response = await service.DeleteModuleAsync(id, cancellationToken);
        await authSessionService.InvalidateSessionCacheAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return ApiOk(response);
    }

    [HttpGet("pages")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetPagesAsync(
        [FromQuery] string versionId,
        [FromQuery] string? moduleId,
        CancellationToken cancellationToken) =>
        ApiOk(await service.GetPagesAsync(versionId, moduleId, cancellationToken));

    [HttpGet("pages/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetPageAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await service.GetPageAsync(id, cancellationToken));

    [HttpPost("pages")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> CreatePageAsync(
        [FromBody] ApplicationDevelopmentPageCreateRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.CreatePageAsync(request, cancellationToken));

    [HttpPut("pages/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> UpdatePageAsync(
        string id,
        [FromBody] ApplicationDevelopmentPageUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.UpdatePageAsync(id, request, cancellationToken));

    [HttpDelete("pages/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerDelete)]
    public async Task<IActionResult> DeletePageAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await service.DeletePageAsync(id, cancellationToken));

    [HttpGet("business-objects")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetBusinessObjectsAsync(
        [FromQuery] string? versionId,
        CancellationToken cancellationToken) =>
        ApiOk(await service.GetBusinessObjectsAsync(versionId, cancellationToken));

    [HttpGet("business-objects/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetBusinessObjectAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await service.GetBusinessObjectAsync(id, cancellationToken));

    [HttpPost("business-objects")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> CreateBusinessObjectAsync(
        [FromBody] ApplicationDevelopmentBusinessObjectUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.CreateBusinessObjectAsync(request, cancellationToken));

    [HttpPut("business-objects/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> UpdateBusinessObjectAsync(
        string id,
        [FromBody] ApplicationDevelopmentBusinessObjectUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateBusinessObjectAsync(id, request, cancellationToken));

    [HttpDelete("business-objects/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> DeleteBusinessObjectAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await service.DeleteBusinessObjectAsync(id, cancellationToken));

    [HttpGet("business-objects/{id}/preview")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    public async Task<IActionResult> PreviewBusinessObjectAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await service.PreviewBusinessObjectAsync(id, cancellationToken));

    [HttpPost("business-objects/{id}/publish")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPublish)]
    public async Task<IActionResult> PublishBusinessObjectAsync(string id, CancellationToken cancellationToken)
    {
        var response = await service.PublishBusinessObjectAsync(id, cancellationToken);
        await authSessionService.InvalidateSessionCacheAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return ApiOk(response);
    }

    [HttpGet("permission-options")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPermissionEdit)]
    public async Task<IActionResult> GetPermissionOptionsAsync(CancellationToken cancellationToken) =>
        ApiOk(await service.GetPermissionOptionsAsync(cancellationToken));

    [HttpGet("pages/{pageId}/preview-schema")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    public async Task<IActionResult> GetPreviewSchemaAsync(string pageId, CancellationToken cancellationToken) =>
        ApiOk(await service.GetPreviewSchemaAsync(pageId, cancellationToken));

    [HttpPost("pages/{pageId}/preview-artifact")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    public async Task<IActionResult> CompilePreviewArtifactAsync(
        string pageId,
        [FromBody] ApplicationDevelopmentPreviewArtifactRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.CompilePreviewArtifactAsync(pageId, request, cancellationToken));

    [HttpPost("pages/{pageId}/environment-check")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    public async Task<IActionResult> CheckPageEnvironmentAsync(
        string pageId,
        [FromBody] ApplicationDevelopmentEnvironmentCheckRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.CheckPageEnvironmentAsync(pageId, request, cancellationToken));

    [HttpPost("pages/{pageId}/refresh-preview-menu")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPreview)]
    public async Task<IActionResult> RefreshPreviewMenuAsync(string pageId, CancellationToken cancellationToken) =>
        ApiOk(await service.RefreshPreviewMenuAsync(pageId, cancellationToken));

    [HttpPost("pages/{pageId}/publish")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPublish)]
    public async Task<IActionResult> PublishPageAsync(string pageId, CancellationToken cancellationToken)
    {
        var response = await service.PublishPageAsync(pageId, cancellationToken);
        await authSessionService.InvalidateSessionCacheAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return ApiOk(response);
    }

    [HttpPost("pages/{pageId}/rollback")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPublish)]
    public async Task<IActionResult> RollbackPageAsync(
        string pageId,
        [FromBody] ApplicationDesignerArtifactRollbackRequest request,
        [FromServices] ApplicationDesignerArtifactRollbackService rollbackService,
        CancellationToken cancellationToken) =>
        ApiOk(await rollbackService.RollbackAsync(pageId, request, HttpContext.TraceIdentifier, cancellationToken));

    [HttpPost("versions/{versionId}/publish")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerPublish)]
    public async Task<IActionResult> PublishVersionAsync(string versionId, CancellationToken cancellationToken)
    {
        var response = await service.PublishVersionAsync(versionId, cancellationToken);
        await authSessionService.InvalidateSessionCacheAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return ApiOk(response);
    }

    [HttpGet("shared-resources")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetSharedResourcesAsync(
        [FromQuery] string? versionId,
        CancellationToken cancellationToken) =>
        ApiOk(await service.GetSharedResourcesAsync(versionId, cancellationToken));

    [HttpGet("shared-resources/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerView)]
    public async Task<IActionResult> GetSharedResourceAsync(string id, CancellationToken cancellationToken) =>
        ApiOk(await service.GetSharedResourceAsync(id, cancellationToken));

    [HttpPost("shared-resources")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> CreateSharedResourceAsync(
        [FromBody] ApplicationDevelopmentSharedResourceUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.CreateSharedResourceAsync(request, cancellationToken));

    [HttpPut("shared-resources/{id}")]
    [Permission(PermissionCodes.AppDevelopmentCenterDesignerEdit)]
    public async Task<IActionResult> UpdateSharedResourceAsync(
        string id,
        [FromBody] ApplicationDevelopmentSharedResourceUpsertRequest request,
        CancellationToken cancellationToken) =>
        ApiOk(await service.UpdateSharedResourceAsync(id, request, cancellationToken));
}
