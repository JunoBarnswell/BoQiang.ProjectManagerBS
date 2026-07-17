using AsterERP.Shared;
using AsterERP.Contracts.Logs;

namespace AsterERP.Api.Infrastructure.Logging;

public interface IOperationLogService
{
    Task<IReadOnlyList<OperationLogResponse>> RecentAsync(int take = 20, CancellationToken cancellationToken = default);

    Task<GridPageResult<OperationLogResponse>> GetPageAsync(OperationLogQueryRequest request, CancellationToken cancellationToken = default);

    Task<OperationLogDetailResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);
}
