namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementOperationWriter
{
    Task CreatePendingAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default);
    Task StartAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default);
    Task<bool> ReportProgressAsync(string operationId, string phase, int progressPercent, CancellationToken cancellationToken = default);
    Task<bool> IsCancellationRequestedAsync(string operationId, CancellationToken cancellationToken = default);
    Task RequestCancellationAsync(string operationId, CancellationToken cancellationToken = default);
    Task CancelAsync(string operationId, CancellationToken cancellationToken = default);
    Task SucceedAsync(string operationId, CancellationToken cancellationToken = default);
    Task CompleteWithImpactAsync(string operationId, string impactJson, CancellationToken cancellationToken = default);
    Task FailAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default);
    Task FailRunningExceptAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default);
}
