using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementExternalApiService
{
    Task<GridPageResult<ProjectManagementProjectResponse>> QueryProjectsAsync(ProjectManagementProjectQuery query, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementTaskListItemResponse>> QueryTasksAsync(string projectId, ProjectManagementExternalTaskQuery query, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementMilestoneResponse>> QueryMilestonesAsync(string projectId, CancellationToken cancellationToken = default);
    Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskDetailResponse>> CreateTaskAsync(string projectId, ProjectManagementExternalTaskWriteRequest request, ProjectManagementExternalApiWriteContext context, CancellationToken cancellationToken = default);
    Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskDetailResponse>> UpdateTaskAsync(string taskId, ProjectManagementExternalTaskWriteRequest request, ProjectManagementExternalApiWriteContext context, CancellationToken cancellationToken = default);
    Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskCommentResponse>> CreateCommentAsync(string taskId, ProjectManagementExternalTaskCommentWriteRequest request, ProjectManagementExternalApiWriteContext context, CancellationToken cancellationToken = default);
    Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskCommentResponse>> UpdateCommentAsync(string taskId, string commentId, ProjectManagementExternalTaskCommentWriteRequest request, ProjectManagementExternalApiWriteContext context, CancellationToken cancellationToken = default);
    Task<ProjectManagementExternalApiWriteResponse<ProjectManagementTaskAttachmentResponse>> CreateAttachmentAsync(string taskId, IFormFile file, string fileSha256, ProjectManagementExternalApiWriteContext context, CancellationToken cancellationToken = default);
}

public sealed record ProjectManagementExternalApiWriteContext(
    string IdempotencyKey,
    string Source,
    string TraceId,
    long? ExpectedVersion = null);
