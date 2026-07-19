using AsterERP.Contracts.ProjectManagement;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementExcelImportConfirmService
{
    Task<ProjectManagementExcelImportResultResponse> ConfirmAsync(
        ProjectManagementExcelImportConfirmRequest request,
        IFormFile file,
        CancellationToken cancellationToken = default);

    Task<ProjectManagementExcelImportResultResponse> GetResultAsync(
        string importId,
        CancellationToken cancellationToken = default);
}
