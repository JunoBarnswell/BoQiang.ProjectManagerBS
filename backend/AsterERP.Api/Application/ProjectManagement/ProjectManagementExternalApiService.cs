using System.Text.Json;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 对外 V1 API 的应用层入口。读取和业务写入都委派给现有项目管理服务，
/// 因而保持会话、权限、项目访问策略及 ORM 数据权限与 UI 同源。
/// </summary>
public sealed class ProjectManagementExternalApiService(
    IProjectManagementProjectService projectService,
    IProjectManagementTaskService taskService,
    IProjectManagementMilestoneService milestoneService,
    IProjectManagementTaskCommentService commentService,
    IProjectManagementTaskAttachmentService attachmentService,
    IProjectManagementExternalApiIdempotencyService idempotencyService) : IProjectManagementExternalApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<GridPageResult<ProjectManagementProjectResponse>> QueryProjectsAsync(ProjectManagementProjectQuery query, CancellationToken cancellationToken = default) =>
        projectService.QueryAsync(query, cancellationToken);

    public Task<GridPageResult<ProjectManagementTaskListItemResponse>> QueryTasksAsync(string projectId, ProjectManagementExternalTaskQuery query, CancellationToken cancellationToken = default) =>
        taskService.QueryAsync(new ProjectManagementTaskQuery(
            Required(projectId, "项目标识不能为空"), query.PageIndex, query.PageSize, query.Keyword, query.Status,
            query.AssigneeUserId, query.ViewKey, query.GroupBy, query.SortBy, query.SortDirection, query.MilestoneId,
            query.ParentTaskId, query.DueFrom, query.DueTo, query.IncludeCompleted, query.LabelFilter), cancellationToken);

    public Task<GridPageResult<ProjectManagementMilestoneResponse>> QueryMilestonesAsync(string projectId, CancellationToken cancellationToken = default) =>
        milestoneService.QueryAsync(Required(projectId, "项目标识不能为空"), cancellationToken);

    public async Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskDetailResponse>> CreateTaskAsync(
        string projectId,
        ProjectManagementExternalTaskWriteRequest request,
        ProjectManagementExternalApiWriteContext context,
        CancellationToken cancellationToken = default)
    {
        var normalizedProjectId = Required(projectId, "项目标识不能为空");
        var normalizedRequest = Required(request, "任务请求不能为空");
        var result = await idempotencyService.ExecuteAsync(
            BuildRequest("task.create", new { projectId = normalizedProjectId, request = normalizedRequest }, context),
            token => taskService.CreateAsync(normalizedProjectId, normalizedRequest.Task, token),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "Task", item.Id),
            cancellationToken);
        return WriteResponse(context, result);
    }

    public async Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskDetailResponse>> UpdateTaskAsync(
        string taskId,
        ProjectManagementExternalTaskWriteRequest request,
        ProjectManagementExternalApiWriteContext context,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = Required(request, "任务请求不能为空");
        var task = WithExpectedVersion(normalizedRequest.Task, context.ExpectedVersion, "任务");
        var result = await idempotencyService.ExecuteAsync(
            BuildRequest("task.update", new { taskId = Required(taskId, "任务标识不能为空"), task }, context),
            token => taskService.UpdateAsync(taskId, task, token),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "Task", item.Id),
            cancellationToken);
        return WriteResponse(context, result);
    }

    public async Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskCommentResponse>> CreateCommentAsync(
        string taskId,
        ProjectManagementExternalTaskCommentWriteRequest request,
        ProjectManagementExternalApiWriteContext context,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = Required(request, "评论请求不能为空");
        var result = await idempotencyService.ExecuteAsync(
            BuildRequest("task-comment.create", new { taskId = Required(taskId, "任务标识不能为空"), request = normalizedRequest }, context),
            token => commentService.CreateAsync(taskId, normalizedRequest.Comment, token),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "TaskComment", item.Id),
            cancellationToken);
        return WriteResponse(context, result);
    }

    public async Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskCommentResponse>> UpdateCommentAsync(
        string taskId,
        string commentId,
        ProjectManagementExternalTaskCommentWriteRequest request,
        ProjectManagementExternalApiWriteContext context,
        CancellationToken cancellationToken = default)
    {
        var normalizedRequest = Required(request, "评论请求不能为空");
        var comment = WithExpectedVersion(normalizedRequest.Comment, context.ExpectedVersion, "评论");
        var result = await idempotencyService.ExecuteAsync(
            BuildRequest("task-comment.update", new { taskId = Required(taskId, "任务标识不能为空"), commentId = Required(commentId, "评论标识不能为空"), comment }, context),
            token => commentService.UpdateAsync(taskId, commentId, comment, token),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "TaskComment", item.Id),
            cancellationToken);
        return WriteResponse(context, result);
    }

    public async Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskAttachmentResponse>> CreateAttachmentAsync(
        string taskId,
        IFormFile file,
        string fileSha256,
        ProjectManagementExternalApiWriteContext context,
        CancellationToken cancellationToken = default)
    {
        if (file is null) throw new ValidationException("附件不能为空", ErrorCodes.ParameterInvalid);
        var normalizedTaskId = Required(taskId, "任务标识不能为空");
        var result = await idempotencyService.ExecuteAsync(
            BuildRequest("task-attachment.create", new { taskId = normalizedTaskId, file.FileName, file.ContentType, file.Length, fileSha256 }, context),
            token => attachmentService.UploadAsync(normalizedTaskId, file, token),
            item => new ProjectManagementExternalApiResource(item.ProjectId, "TaskAttachment", item.Id),
            cancellationToken);
        return WriteResponse(context, result);
    }

    private static ProjectManagementExternalApiIdempotencyRequest BuildRequest(string operation, object payload, ProjectManagementExternalApiWriteContext context) =>
        new(operation, context.IdempotencyKey, ProjectManagementExternalApiIdempotencyService.HashPayload(JsonSerializer.Serialize(payload, JsonOptions)), context.Source, context.TraceId);

    private static ProjectManagementExternalApiWriteResponse<T> WriteResponse<T>(ProjectManagementExternalApiWriteContext context, ProjectManagementExternalApiIdempotencyResult<T> result) =>
        new(ProjectManagementExternalApiContract.ApiVersion, context.IdempotencyKey, result.Replayed, context.TraceId, result.Result);

    private static ProjectManagementTaskUpsertRequest WithExpectedVersion(ProjectManagementTaskUpsertRequest request, long? expectedVersion, string resourceName)
    {
        var version = RequireExpectedVersion(expectedVersion, request.VersionNo, resourceName);
        return request with { VersionNo = version };
    }

    private static ProjectManagementTaskCommentUpsertRequest WithExpectedVersion(ProjectManagementTaskCommentUpsertRequest request, long? expectedVersion, string resourceName)
    {
        var version = RequireExpectedVersion(expectedVersion, request.VersionNo, resourceName);
        return request with { VersionNo = version };
    }

    private static long RequireExpectedVersion(long? expectedVersion, long requestVersion, string resourceName)
    {
        if (!expectedVersion.HasValue || expectedVersion.Value <= 0)
            throw new ValidationException($"更新{resourceName}必须提供 If-Match 版本", ErrorCodes.ParameterInvalid);
        if (requestVersion > 0 && requestVersion != expectedVersion.Value)
            throw new ValidationException($"If-Match 与请求中的 VersionNo 不一致", ErrorCodes.ParameterInvalid);
        return expectedVersion.Value;
    }

    private static T Required<T>(T? value, string message) where T : class => value ?? throw new ValidationException(message, ErrorCodes.ParameterInvalid);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message, ErrorCodes.ParameterInvalid) : value.Trim();
}
