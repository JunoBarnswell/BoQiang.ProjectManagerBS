using System.Text.Json;
using AsterERP.Api.Application.AsterScene;
using AsterERP.Contracts.AsterScene;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/asterscene/projects")]
public sealed class AsterSceneProjectsController(AsterSceneDocumentService documentService) : BaseApiController
{
    [HttpGet]
    [Permission(PermissionCodes.AsterSceneProjectList)]
    public async Task<IActionResult> GetProjectsAsync([FromQuery] AsterSceneGridQuery query, CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.GetProjectsAsync(query, cancellationToken));
    }

    [HttpPost]
    [Permission(PermissionCodes.AsterSceneProjectCreate)]
    public async Task<IActionResult> CreateProjectAsync([FromBody] AsterSceneCreateProjectRequest request, CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.CreateProjectAsync(request, cancellationToken));
    }

    [HttpPut("{projectId}")]
    [Permission(PermissionCodes.AsterSceneProjectEdit)]
    public async Task<IActionResult> UpdateProjectAsync(
        string projectId,
        [FromBody] AsterSceneUpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.UpdateProjectAsync(projectId, request, cancellationToken));
    }

    [HttpGet("{projectId}/document")]
    [Permission(PermissionCodes.AsterSceneStudioOpen)]
    public async Task<IActionResult> GetDocumentAsync(string projectId, CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.GetDocumentAsync(projectId, cancellationToken));
    }

    [HttpPut("{projectId}/document")]
    [Permission(PermissionCodes.AsterSceneDocumentSave)]
    public async Task<IActionResult> SaveDocumentAsync(
        string projectId,
        [FromBody] AsterSceneSaveDocumentRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.SaveDocumentAsync(projectId, request, cancellationToken));
    }

    [HttpGet("{projectId}/document/versions")]
    [Permission(PermissionCodes.AsterSceneStudioOpen)]
    public async Task<IActionResult> GetDocumentVersionsAsync(
        string projectId,
        [FromQuery] AsterSceneGridQuery query,
        CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.GetDocumentVersionsAsync(projectId, query, cancellationToken));
    }

    [HttpGet("{projectId}/document/versions/{revision:int}")]
    [Permission(PermissionCodes.AsterSceneStudioOpen)]
    public async Task<IActionResult> GetDocumentVersionAsync(
        string projectId,
        int revision,
        CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.GetDocumentVersionAsync(projectId, revision, cancellationToken));
    }

    [HttpPost("{projectId}/document/versions/{revision:int}/restore")]
    [Permission(PermissionCodes.AsterSceneDocumentSave)]
    public async Task<IActionResult> RestoreDocumentVersionAsync(
        string projectId,
        int revision,
        [FromBody] AsterSceneRestoreDocumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await documentService.RestoreDocumentVersionAsync(projectId, revision, request, cancellationToken));
    }

    [HttpPost("{projectId}/document/validate")]
    [Permission(PermissionCodes.AsterSceneStudioOpen)]
    public async Task<IActionResult> ValidateDocumentAsync(
        string projectId,
        [FromBody] JsonElement document,
        CancellationToken cancellationToken)
    {
        _ = projectId;
        return ApiOk(await documentService.ValidateDocumentAsync(document, cancellationToken));
    }

    [HttpDelete("{projectId}")]
    [Permission(PermissionCodes.AsterSceneProjectDelete)]
    public async Task<IActionResult> DeleteProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        await documentService.DeleteProjectAsync(projectId, cancellationToken);
        return ApiOk(true);
    }
}
