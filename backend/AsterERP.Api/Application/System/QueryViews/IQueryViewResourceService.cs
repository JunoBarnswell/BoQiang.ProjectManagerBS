using AsterERP.Contracts.System.QueryViews;

namespace AsterERP.Api.Application.System.QueryViews;

public interface IQueryViewResourceService
{
    Task<QueryViewResourceSyncResponse> SyncAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QueryViewTableResourceResponse>> GetTablesAsync(CancellationToken cancellationToken = default);

    Task SetTableEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default);

    Task SetColumnEnabledAsync(string id, bool enabled, CancellationToken cancellationToken = default);
}
