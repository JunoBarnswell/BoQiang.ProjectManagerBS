using AsterERP.Api.Infrastructure.Security;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.System.Printing;

public sealed class PrintWorkspaceResolver(ICurrentUser currentUser)
{
    public PrintWorkspaceScope GetRequiredCurrent()
    {
        if (!currentUser.IsAsterErpAuthenticated())
        {
            throw new InvalidOperationException("当前用户未登录，无法解析打印工作区。");
        }

        var tenantId = currentUser.GetAsterErpTenantId();
        var appCode = currentUser.GetAsterErpAppCode();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
        {
            throw new InvalidOperationException("当前未选择租户应用工作区，无法解析打印工作区。");
        }

        return new PrintWorkspaceScope(
            tenantId,
            appCode.ToUpperInvariant(),
            currentUser.GetAsterErpUserId(),
            currentUser.UserName ?? currentUser.GetAsterErpUserId());
    }
}
