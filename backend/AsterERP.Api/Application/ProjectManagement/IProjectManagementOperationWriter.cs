namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementOperationWriter
{
    Task StartAsync(string operationId, string operationType, string impactJson, string traceId, CancellationToken cancellationToken = default);
    Task SucceedAsync(string operationId, CancellationToken cancellationToken = default);
    Task FailAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default);
    Task FailRunningExceptAsync(string operationId, string errorMessage, CancellationToken cancellationToken = default);
}
