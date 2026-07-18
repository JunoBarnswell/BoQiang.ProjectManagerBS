using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.ProjectManagement;

public enum ProjectManagementFileWritePurpose
{
    TaskAttachment,
    SyncImport
}

public sealed record ProjectManagementFileUploadContext(ProjectManagementFileWritePurpose Purpose, string? TaskId = null);

public sealed record ProjectManagementStoredFile(string Id, string FileName, long Size);

public interface IProjectManagementFileStore
{
    Task<ProjectManagementStoredFile> StoreAsync(IFormFile file, ProjectManagementFileUploadContext context, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken = default);

    Task DeleteAsync(string fileId, CancellationToken cancellationToken = default);
}
