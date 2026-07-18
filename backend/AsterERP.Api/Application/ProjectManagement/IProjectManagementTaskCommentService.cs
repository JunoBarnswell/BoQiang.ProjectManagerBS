using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskCommentService
{
    Task<IReadOnlyList<ProjectManagementTaskCommentResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementTaskCommentMentionCandidateResponse>> QueryMentionCandidatesAsync(string taskId, ProjectManagementTaskCommentMentionCandidateQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskCommentResponse> CreateAsync(string taskId, ProjectManagementTaskCommentUpsertRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskCommentResponse> UpdateAsync(string taskId, string id, ProjectManagementTaskCommentUpsertRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
}
