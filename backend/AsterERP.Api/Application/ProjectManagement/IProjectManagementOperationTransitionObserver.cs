using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementOperationTransitionObserver
{
    Task BeforePersistAsync(ProjectManagementOperationEntity operation, CancellationToken cancellationToken = default);
}
