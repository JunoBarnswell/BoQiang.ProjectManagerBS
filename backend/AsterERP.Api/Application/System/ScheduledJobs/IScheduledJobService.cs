using AsterERP.Shared;
using AsterERP.Contracts.System.ScheduledJobs;

namespace AsterERP.Api.Application.System.ScheduledJobs;

public interface IScheduledJobService
{
    Task<GridPageResult<ScheduledJobListItemResponse>> GetPageAsync(GridQuery gridQuery, string? jobType, string? result, CancellationToken cancellationToken = default);

    Task<ScheduledJobSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<ScheduledJobTypesResponse> GetTypesAsync(CancellationToken cancellationToken = default);

    Task<ScheduledJobDetailResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<ScheduledJobListItemResponse> CreateAsync(ScheduledJobUpsertRequest request, CancellationToken cancellationToken = default);

    Task<ScheduledJobListItemResponse> UpdateAsync(string id, ScheduledJobUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task PauseAsync(string id, CancellationToken cancellationToken = default);

    Task ResumeAsync(string id, CancellationToken cancellationToken = default);

    Task<string> TriggerAsync(string id, CancellationToken cancellationToken = default);

    Task<GridPageResult<ScheduledJobLogResponse>> GetLogsAsync(string id, GridQuery gridQuery, string? result, CancellationToken cancellationToken = default);

    Task SynchronizeAllAsync(CancellationToken cancellationToken = default);
}
