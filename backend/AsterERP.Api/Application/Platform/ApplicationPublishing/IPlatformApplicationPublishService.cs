using AsterERP.Contracts.Platform;
using AsterERP.Shared;

namespace AsterERP.Api.Application.Platform.ApplicationPublishing;

public interface IPlatformApplicationPublishService
{
    Task<ApplicationPublishTaskResponse> PublishAsync(
        string appId,
        ApplicationPublishRequest request,
        CancellationToken cancellationToken = default);

    Task<GridPageResult<ApplicationPublishTaskResponse>> GetTasksAsync(
        string appId,
        GridQuery gridQuery,
        CancellationToken cancellationToken = default);

    Task<ApplicationPublishTaskResponse> GetTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<GridPageResult<ApplicationPublishLogResponse>> GetLogsAsync(
        string taskId,
        GridQuery gridQuery,
        CancellationToken cancellationToken = default);

    Task<GridPageResult<ApplicationPublishArtifactResponse>> GetArtifactsAsync(
        string appId,
        GridQuery gridQuery,
        CancellationToken cancellationToken = default);

    Task<ApplicationPublishArtifactResponse> PackageTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<(ApplicationPublishArtifactResponse Metadata, Stream Stream)> DownloadArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default);

    Task DeleteArtifactAsync(
        string artifactId,
        CancellationToken cancellationToken = default);
}
