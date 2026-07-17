using AsterERP.Api.Application.Platform.ApplicationPublishing;
using AsterERP.Contracts.Platform;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;

namespace AsterERP.Api.Controllers;

[Route("api/platform")]
public sealed class PlatformApplicationPublishController(
    IPlatformApplicationPublishService publishService) : BaseApiController
{
    [HttpPost("applications/{id}/publish")]
    [Permission(PermissionCodes.PlatformApplicationPublish)]
    public async Task<IActionResult> PublishAsync(
        string id,
        [FromBody] ApplicationPublishRequest request,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.PublishAsync(id, request, cancellationToken));
    }

    [HttpGet("applications/{id}/publish-tasks")]
    [Permission(PermissionCodes.PlatformApplicationPublishTask)]
    public async Task<IActionResult> GetTasksAsync(
        string id,
        [FromQuery] GridQuery gridQuery,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetTasksAsync(id, gridQuery, cancellationToken));
    }

    [HttpGet("application-publish-tasks/{taskId}")]
    [Permission(PermissionCodes.PlatformApplicationPublishTask)]
    public async Task<IActionResult> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetTaskAsync(taskId, cancellationToken));
    }

    [HttpGet("application-publish-tasks/{taskId}/logs")]
    [Permission(PermissionCodes.PlatformApplicationPublishLog)]
    public async Task<IActionResult> GetLogsAsync(
        string taskId,
        [FromQuery] GridQuery gridQuery,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetLogsAsync(taskId, gridQuery, cancellationToken));
    }

    [HttpPost("application-publish-tasks/{taskId}/package")]
    [Permission(PermissionCodes.PlatformApplicationPublish)]
    public async Task<IActionResult> PackageTaskAsync(
        string taskId,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.PackageTaskAsync(taskId, cancellationToken));
    }

    [HttpGet("applications/{id}/publish-artifacts")]
    [Permission(PermissionCodes.PlatformApplicationPublishArtifactDownload)]
    public async Task<IActionResult> GetArtifactsAsync(
        string id,
        [FromQuery] GridQuery gridQuery,
        CancellationToken cancellationToken)
    {
        return ApiOk(await publishService.GetArtifactsAsync(id, gridQuery, cancellationToken));
    }

    [HttpGet("application-publish-artifacts/{artifactId}/download")]
    [Permission(PermissionCodes.PlatformApplicationPublishArtifactDownload)]
    public async Task<IActionResult> DownloadArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken)
    {
        var result = await publishService.DownloadArtifactAsync(artifactId, cancellationToken);
        return File(result.Stream, result.Metadata.ContentType, result.Metadata.FileName);
    }

    [HttpDelete("application-publish-artifacts/{artifactId}")]
    [Permission(PermissionCodes.PlatformApplicationPublishArtifactDelete)]
    public async Task<IActionResult> DeleteArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken)
    {
        await publishService.DeleteArtifactAsync(artifactId, cancellationToken);
        return ApiOk(true);
    }
}
