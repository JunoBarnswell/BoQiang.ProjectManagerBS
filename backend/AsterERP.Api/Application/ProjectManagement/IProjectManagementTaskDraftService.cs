using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskDraftService
{
    Task<ProjectManagementTaskDraftResponse> CreateAsync(ProjectManagementTaskDraftCreateRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDraftResponse> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskDraftAttachmentResponse> UploadAsync(string id, IFormFile file, CancellationToken cancellationToken = default);
    Task BindAsync(string id, string taskId, string projectId, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
