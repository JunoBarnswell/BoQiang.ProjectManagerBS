using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementAuditService
{
    Task<GridPageResult<ProjectManagementAuditItem>> QueryAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementAuditDetail> GetDetailAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementAuditExportResponse> ExportAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default);
    Task<GridPageResult<ProjectManagementOperationItem>> QueryOperationsAsync(ProjectManagementOperationQuery query, CancellationToken cancellationToken = default);
}
