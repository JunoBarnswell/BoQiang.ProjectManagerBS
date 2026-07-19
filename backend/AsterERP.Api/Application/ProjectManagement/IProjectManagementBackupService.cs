using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementBackupService
{
    Task<ProjectManagementBackupResponse> CreateAsync(ProjectManagementBackupRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementBackupResponse>> ListAsync(CancellationToken cancellationToken = default);
    Task<ProjectManagementBackupDownload> DownloadAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, ProjectManagementBackupDeleteRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementBackupRestorePreviewResponse> PreviewRestoreAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementBackupResponse> RestoreAsync(string id, ProjectManagementRestoreRequest request, CancellationToken cancellationToken = default);
}
