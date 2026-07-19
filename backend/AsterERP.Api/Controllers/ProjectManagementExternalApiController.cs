using System.Security.Cryptography;
using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Infrastructure;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AsterERP.Api.Controllers;

/// <summary>供 OAuth/会话已授权调用方使用的项目管理 V1 API。</summary>
[ApiController]
[Route("api/project-management/external/v1")]
[EnableRateLimiting(AuthenticationRateLimitPolicy.ProjectManagementExternalApiName)]
public sealed class ProjectManagementExternalApiController(IProjectManagementExternalApiService service) : BaseApiController, IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers["X-API-Version"] = ProjectManagementExternalApiContract.ApiVersion;
    }

    public void OnActionExecuted(ActionExecutedContext context) { }

    [HttpGet("projects")]
    [Permission(PermissionCodes.ProjectManagementProjectView)]
    [ProducesResponseType(typeof(ApiResult<GridPageResult<ProjectManagementProjectResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryProjectsAsync([FromQuery] ProjectManagementProjectQuery query, CancellationToken cancellationToken) =>
        ApiVersionedOk(await service.QueryProjectsAsync(query, cancellationToken));

    [HttpGet("projects/{projectId}/tasks")]
    [Permission(PermissionCodes.ProjectManagementTaskView)]
    [ProducesResponseType(typeof(ApiResult<GridPageResult<ProjectManagementTaskListItemResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryTasksAsync(string projectId, [FromQuery] ProjectManagementExternalTaskQuery query, CancellationToken cancellationToken) =>
        ApiVersionedOk(await service.QueryTasksAsync(projectId, query, cancellationToken));

    [HttpGet("projects/{projectId}/milestones")]
    [Permission(PermissionCodes.ProjectManagementMilestoneView)]
    [ProducesResponseType(typeof(ApiResult<GridPageResult<ProjectManagementMilestoneResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryMilestonesAsync(string projectId, CancellationToken cancellationToken) =>
        ApiVersionedOk(await service.QueryMilestonesAsync(projectId, cancellationToken));

    [HttpPost("projects/{projectId}/tasks")]
    [Permission(PermissionCodes.ProjectManagementTaskAdd)]
    [ProducesResponseType(typeof(ApiResult<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskDetailResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTaskAsync(
        string projectId,
        [FromBody] ProjectManagementExternalTaskWriteRequest request,
        [FromHeader(Name = ProjectManagementExternalApiContract.IdempotencyKeyHeader)] string? idempotencyKey,
        [FromHeader(Name = ProjectManagementExternalApiContract.SourceHeader)] string? source,
        CancellationToken cancellationToken)
    {
        try { return ApiVersionedOk(await service.CreateTaskAsync(projectId, request, WriteContext(idempotencyKey, source), cancellationToken)); }
        catch (ProjectManagementExternalApiIdempotencyConflictException exception) { return IdempotencyConflict(exception); }
    }

    [HttpPut("tasks/{taskId}")]
    [Permission(PermissionCodes.ProjectManagementTaskEdit)]
    [ProducesResponseType(typeof(ApiResult<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskDetailResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTaskAsync(
        string taskId,
        [FromBody] ProjectManagementExternalTaskWriteRequest request,
        [FromHeader(Name = ProjectManagementExternalApiContract.IdempotencyKeyHeader)] string? idempotencyKey,
        [FromHeader(Name = ProjectManagementExternalApiContract.VersionHeader)] string? version,
        [FromHeader(Name = ProjectManagementExternalApiContract.SourceHeader)] string? source,
        CancellationToken cancellationToken)
    {
        try { return ApiVersionedOk(await service.UpdateTaskAsync(taskId, request, WriteContext(idempotencyKey, source, version), cancellationToken)); }
        catch (ProjectManagementExternalApiIdempotencyConflictException exception) { return IdempotencyConflict(exception); }
    }

    [HttpPost("tasks/{taskId}/comments")]
    [Permission(PermissionCodes.ProjectManagementCommentAdd)]
    [ProducesResponseType(typeof(ApiResult<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskCommentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateCommentAsync(
        string taskId,
        [FromBody] ProjectManagementExternalTaskCommentWriteRequest request,
        [FromHeader(Name = ProjectManagementExternalApiContract.IdempotencyKeyHeader)] string? idempotencyKey,
        [FromHeader(Name = ProjectManagementExternalApiContract.SourceHeader)] string? source,
        CancellationToken cancellationToken)
    {
        try { return ApiVersionedOk(await service.CreateCommentAsync(taskId, request, WriteContext(idempotencyKey, source), cancellationToken)); }
        catch (ProjectManagementExternalApiIdempotencyConflictException exception) { return IdempotencyConflict(exception); }
    }

    [HttpPut("tasks/{taskId}/comments/{commentId}")]
    [Permission(PermissionCodes.ProjectManagementCommentAdd)]
    [ProducesResponseType(typeof(ApiResult<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskCommentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCommentAsync(
        string taskId,
        string commentId,
        [FromBody] ProjectManagementExternalTaskCommentWriteRequest request,
        [FromHeader(Name = ProjectManagementExternalApiContract.IdempotencyKeyHeader)] string? idempotencyKey,
        [FromHeader(Name = ProjectManagementExternalApiContract.VersionHeader)] string? version,
        [FromHeader(Name = ProjectManagementExternalApiContract.SourceHeader)] string? source,
        CancellationToken cancellationToken)
    {
        try { return ApiVersionedOk(await service.UpdateCommentAsync(taskId, commentId, request, WriteContext(idempotencyKey, source, version), cancellationToken)); }
        catch (ProjectManagementExternalApiIdempotencyConflictException exception) { return IdempotencyConflict(exception); }
    }

    [HttpPost("tasks/{taskId}/attachments")]
    [Permission(PermissionCodes.ProjectManagementAttachmentManage)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResult<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskAttachmentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResult<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateAttachmentAsync(
        string taskId,
        [FromForm] IFormFile file,
        [FromHeader(Name = ProjectManagementExternalApiContract.IdempotencyKeyHeader)] string? idempotencyKey,
        [FromHeader(Name = ProjectManagementExternalApiContract.SourceHeader)] string? source,
        CancellationToken cancellationToken)
    {
        try { return ApiVersionedOk(await service.CreateAttachmentAsync(taskId, file, await CalculateSha256Async(file, cancellationToken), WriteContext(idempotencyKey, source), cancellationToken)); }
        catch (ProjectManagementExternalApiIdempotencyConflictException exception) { return IdempotencyConflict(exception); }
    }

    private IActionResult ApiVersionedOk<T>(T data)
    {
        return ApiOk(data);
    }

    private ProjectManagementExternalApiWriteContext WriteContext(string? idempotencyKey, string? source, string? version = null) =>
        new(Required(idempotencyKey, "Idempotency-Key 不能为空"), source?.Trim() ?? "external-api", HttpContext.TraceIdentifier, ParseVersion(version));

    private IActionResult IdempotencyConflict(ProjectManagementExternalApiIdempotencyConflictException exception) =>
        StatusCode(StatusCodes.Status409Conflict, ApiResultFactory.Fail<object?>(exception.Message, HttpContext.TraceIdentifier, ErrorCodes.ProjectManagementIdempotencyConflict));

    private static long? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim().Trim('"');
        return long.TryParse(normalized, out var version) && version > 0
            ? version
            : throw new AsterERP.Shared.Exceptions.ValidationException("If-Match 必须是正整数版本", ErrorCodes.ParameterInvalid);
    }

    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value)
        ? throw new AsterERP.Shared.Exceptions.ValidationException(message, ErrorCodes.ParameterInvalid)
        : value.Trim();

    private static async Task<string> CalculateSha256Async(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null) throw new AsterERP.Shared.Exceptions.ValidationException("附件不能为空", ErrorCodes.ParameterInvalid);
        await using var stream = file.OpenReadStream();
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
