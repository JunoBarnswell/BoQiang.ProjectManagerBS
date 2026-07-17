using AsterERP.Shared;
using AsterERP.Contracts.System.Menus;

namespace AsterERP.Api.Application.System.Menus;

public interface ISystemMenuService
{
    Task<GridPageResult<MenuListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<MenuListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuTreeNodeResponse>> GetTreeAsync(GridQuery? gridQuery = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuTreeNodeResponse>> GetVisibleTreeAsync(
        IReadOnlyList<string> permissionCodes,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);

    Task<MenuListItemResponse> CreateAsync(MenuUpsertRequest request, CancellationToken cancellationToken = default);

    Task<MenuListItemResponse> UpdateAsync(string id, MenuUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);
}
