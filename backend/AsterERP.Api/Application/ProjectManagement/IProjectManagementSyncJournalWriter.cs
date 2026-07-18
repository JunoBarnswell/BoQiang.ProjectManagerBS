namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementSyncJournalWriter
{
    Task AppendAsync(ProjectManagementSyncJournalEvent entry, CancellationToken cancellationToken = default);
}
