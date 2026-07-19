using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementAuditGovernanceService
{
    Task<ProjectManagementAuditGovernancePolicy> GetPolicyAsync(CancellationToken cancellationToken = default);
    Task<ProjectManagementOperationResponse> StartCleanupAsync(CancellationToken cancellationToken = default);
    Task ExecuteCleanupAsync(string operationId, CancellationToken cancellationToken = default);
}
