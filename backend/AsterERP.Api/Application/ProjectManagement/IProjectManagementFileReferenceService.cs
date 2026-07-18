namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementFileReferenceService
{
    Task<ProjectManagementFileReference> ValidateAsync(
        ProjectManagementFileReference reference,
        string tenantId,
        string appCode,
        string traceId,
        CancellationToken cancellationToken = default);
}
