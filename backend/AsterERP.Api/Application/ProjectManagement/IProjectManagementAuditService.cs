using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementAuditService
{
    Task<GridPageResult<ProjectManagementAuditItem>> QueryAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementAuditDetail> GetDetailAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementAuditExportResponse> ExportAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementAuditExportStartResponse> StartExportAsync(ProjectManagementAuditExportRequest request, CancellationToken cancellationToken = default);
    Task ExecuteExportAsync(string operationId, CancellationToken cancellationToken = default);
    Task<ProjectManagementAuditExportResponse> DownloadExportAsync(string operationId, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementOperationItem>> QueryOperationsAsync(ProjectManagementOperationQuery query, CancellationToken cancellationToken = default);
}
