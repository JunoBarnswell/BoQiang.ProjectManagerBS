namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementMaintenanceLock
{
    Task<string> AcquireAsync(string lockKey, TimeSpan duration, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string operationId, CancellationToken cancellationToken = default);
}
