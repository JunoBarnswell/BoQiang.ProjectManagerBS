using AsterERP.Contracts.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public interface IProjectManagementSyncService
{
    Task<(byte[] Content, string FileName)> ExportAsync(ProjectManagementSyncExportRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncPreviewResponse> PreviewAsync(Stream packageStream, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncImportResponse> ImportAsync(Stream packageStream, ProjectManagementSyncImportRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncWatermarkResponse> GetWatermarkAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectManagementSyncJournalItem>> GetChangesAsync(string? projectId, long sinceSequenceNo, int limit, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncWatermarkResponse> AcknowledgeAsync(ProjectManagementSyncAcknowledgeRequest request, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncHistoryPage> GetHistoryAsync(ProjectManagementSyncHistoryQuery query, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncHistoryDetail> GetHistoryDetailAsync(string id, CancellationToken cancellationToken = default);
    Task<(string FileName, byte[] Content)> DownloadHistoryReportAsync(string id, CancellationToken cancellationToken = default);
    Task<ProjectManagementSyncImportResponse> RetryAsync(string historyId, Stream packageStream, ProjectManagementSyncImportRequest request, CancellationToken cancellationToken = default);
}
