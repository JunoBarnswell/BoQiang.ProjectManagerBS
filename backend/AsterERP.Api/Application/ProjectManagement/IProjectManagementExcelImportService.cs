using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementExcelImportService
{
    Task<ProjectManagementExcelTemplateFile> DownloadTemplateAsync(CancellationToken cancellationToken = default);

    Task<ProjectManagementExcelImportPreviewResponse> PreviewAsync(IFormFile file, CancellationToken cancellationToken = default);

    Task<ProjectManagementExcelImportSnapshot> CreateSnapshotAsync(IFormFile file, CancellationToken cancellationToken = default);
}
