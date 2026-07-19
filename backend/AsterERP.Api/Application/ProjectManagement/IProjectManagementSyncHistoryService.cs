using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementSyncHistoryService
{
    Task RecordAsync(ProjectManagementSyncHistoryRecord record, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncHistoryPage> QueryAsync(ProjectManagementSyncHistoryQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncHistoryDetail> GetAsync(string id, CancellationToken cancellationToken = default);
    Task<(string FileName, byte[] Content)> DownloadSafeReportAsync(string id, CancellationToken cancellationToken = default);
}

public sealed record ProjectManagementSyncHistoryRecord(
    string OperationType,
    string PackageId,
    string SourceTenantId,
    string SourceAppCode,
    string? SourceDeviceId,
    string Status,
    ProjectManagementSyncImportResponse Result,
    string? ErrorMessage = null,
    string? RetryOfHistoryId = null);
