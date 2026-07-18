namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementActivityWriter
{
    Task AppendAsync(
        ProjectManagementActivityEvent activity,
        CancellationToken cancellationToken = default);
}
