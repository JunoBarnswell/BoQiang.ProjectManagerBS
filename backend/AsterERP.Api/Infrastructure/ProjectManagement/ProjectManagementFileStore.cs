using AsterERP.Api.Application.ProjectManagement;
using AsterERP.Api.Application.System.Files;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Infrastructure.ProjectManagement;

public sealed class ProjectManagementFileStore(IFileAppService fileAppService) : IProjectManagementFileStore
{
    public async Task<ProjectManagementStoredFile> StoreAsync(IFormFile file, ProjectManagementFileUploadContext context, CancellationToken cancellationToken = default)
    {
        var uploaded = await fileAppService.UploadAsync(file, ToRemark(context), cancellationToken);
        return new ProjectManagementStoredFile(uploaded.Id, uploaded.FileName, uploaded.Size);
    }

    public async Task<Stream> OpenReadAsync(string fileId, CancellationToken cancellationToken = default)
        => (await fileAppService.DownloadAsync(fileId, cancellationToken)).Stream;

    public Task DeleteAsync(string fileId, CancellationToken cancellationToken = default)
        => fileAppService.DeleteAsync(fileId, cancellationToken);

    private static string ToRemark(ProjectManagementFileUploadContext context) => context.Purpose switch
    {
        ProjectManagementFileWritePurpose.TaskAttachment when !string.IsNullOrWhiteSpace(context.TaskId) => $"ProjectManagement task:{context.TaskId}",
        ProjectManagementFileWritePurpose.SyncImport => "ProjectManagement sync import",
        ProjectManagementFileWritePurpose.TaskAttachment => throw new ArgumentException("任务附件必须提供任务标识", nameof(context)),
        _ => throw new ArgumentOutOfRangeException(nameof(context))
    };
}
