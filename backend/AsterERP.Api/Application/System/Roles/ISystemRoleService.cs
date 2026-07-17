using AsterERP.Shared;
using AsterERP.Contracts.System.Roles;
using AsterERP.Contracts.System.Menus;

namespace AsterERP.Api.Application.System.Roles;

public interface ISystemRoleService
{
    Task<GridPageResult<RoleListItemResponse>> GetPageAsync(GridQuery gridQuery, CancellationToken cancellationToken = default);

    Task<RoleListItemResponse> GetDetailAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RolePermissionCatalogItemResponse>> GetPermissionCatalogAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRolePermissionCodesAsync(string roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MenuTreeNodeResponse>> GetPermissionTreeAsync(GridQuery? gridQuery = null, CancellationToken cancellationToken = default);

    Task<RoleListItemResponse> CreateAsync(RoleUpsertRequest request, CancellationToken cancellationToken = default);

    Task<RoleListItemResponse> UpdateAsync(string id, RoleUpsertRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task BatchDeleteAsync(IReadOnlyList<string> ids, CancellationToken cancellationToken = default);

    Task BatchUpdateStatusAsync(IReadOnlyList<string> ids, string status, CancellationToken cancellationToken = default);

    Task UpdatePermissionsAsync(string roleId, RolePermissionUpdateRequest request, CancellationToken cancellationToken = default);
}
