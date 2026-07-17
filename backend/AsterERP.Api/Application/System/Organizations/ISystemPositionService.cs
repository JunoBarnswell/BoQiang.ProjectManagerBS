using AsterERP.Shared;
using AsterERP.Contracts.System.Organizations;

namespace AsterERP.Api.Application.System.Organizations;

public interface ISystemPositionService
{
    Task<GridPageResult<PositionListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<PositionListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<PositionListItemResponse> CreateAsync(PositionUpsertRequest request, CancellationToken cancellationToken = default);

    Task<PositionListItemResponse> UpdateAsync(string id, PositionUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
