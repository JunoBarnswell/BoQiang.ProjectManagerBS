using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.System.Menus;

namespace AsterERP.Api.Application.Auth;

public interface IWorkspaceMenuReader
{
    Task<IReadOnlyList<MenuTreeNodeResponse>> GetVisibleTreeAsync(
        SystemUserEntity user,
        IReadOnlyList<string> permissionCodes,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken = default);
}
