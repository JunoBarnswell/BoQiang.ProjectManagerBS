using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementPurgeFileDeletionService
{
    Task ScheduleAsync(ISqlSugarClient db, string operationId, IReadOnlyCollection<ProjectManagementTaskAttachmentEntity> attachments, CancellationToken cancellationToken = default);
    Task<bool> TryProcessAsync(string operationId, CancellationToken cancellationToken = default);
}
