using AsterERP.Shared;
using AsterERP.Contracts.System.Dicts;

namespace AsterERP.Api.Application.System.Dicts;

public interface IDictManagementService
{
    Task<GridPageResult<DictTypeListItemResponse>> GetTypesPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<DictTypeListItemResponse> GetTypeDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DictItemListItemResponse>> GetItemsAsync(string dictTypeId, CancellationToken cancellationToken = default);

    Task<GridPageResult<DictItemListItemResponse>> GetItemsPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<DictItemListItemResponse> GetItemDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<DictTypeListItemResponse> CreateTypeAsync(DictTypeUpsertRequest request, CancellationToken cancellationToken = default);

    Task<DictTypeListItemResponse> UpdateTypeAsync(string id, DictTypeUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteTypeAsync(string id, CancellationToken cancellationToken = default);

    Task<DictItemListItemResponse> CreateItemAsync(string dictTypeId, DictItemUpsertRequest request, CancellationToken cancellationToken = default);

    Task<DictItemListItemResponse> UpdateItemAsync(string id, DictItemUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteItemAsync(string id, CancellationToken cancellationToken = default);
}
