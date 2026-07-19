using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.Http;

using AsterERP.Api.Application.System.Files;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementTaskAttachmentService
{
    Task<IReadOnlyList<ProjectManagementTaskAttachmentResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default);
    Task<ProjectManagementTaskAttachmentResponse> UploadAsync(string taskId, IFormFile file, CancellationToken cancellationToken = default);
    Task<(ProjectManagementTaskAttachmentResponse Metadata, Stream Stream)> DownloadAsync(string taskId, string id, CancellationToken cancellationToken = default);
    Task<(ProjectManagementTaskAttachmentResponse Metadata, FilePreviewStreamResult Preview)> PreviewAsync(string taskId, string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default);
}
