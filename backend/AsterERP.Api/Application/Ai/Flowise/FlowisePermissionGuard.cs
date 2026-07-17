using AsterERP.Api.Infrastructure.Security;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowisePermissionGuard(ICurrentUser currentUser)
{
    public void EnsureSecretReveal()
    {
        EnsureAny(PermissionCodes.FlowiseRevealSecret, PermissionCodes.FlowiseManage);
    }

    public void EnsureImport()
    {
        EnsureAny(PermissionCodes.FlowiseImport, PermissionCodes.FlowiseManage);
    }

    public void EnsureExport()
    {
        EnsureAny(PermissionCodes.FlowiseExport, PermissionCodes.FlowiseManage, PermissionCodes.FlowiseView);
    }

    public void EnsureManage()
    {
        EnsureAny(PermissionCodes.FlowiseManage);
    }

    public void EnsureAnyView()
    {
        EnsureAny(
            PermissionCodes.FlowiseView,
            PermissionCodes.FlowiseManage,
            PermissionCodes.FlowiseChatflowsView,
            PermissionCodes.FlowiseAgentflowsView,
            PermissionCodes.FlowiseExecutionsView,
            PermissionCodes.FlowiseAssistantsView,
            PermissionCodes.FlowiseMarketplacesView,
            PermissionCodes.FlowiseToolsView,
            PermissionCodes.FlowiseCredentialsView,
            PermissionCodes.FlowiseVariablesView,
            PermissionCodes.FlowiseApiKeysView,
            PermissionCodes.FlowiseDocumentStoresView,
            PermissionCodes.FlowiseDatasetsView,
            PermissionCodes.FlowiseEvaluatorsView,
            PermissionCodes.FlowiseEvaluationsView,
            PermissionCodes.FlowiseSsoManage,
            PermissionCodes.FlowiseRolesManage,
            PermissionCodes.FlowiseUsersManage,
            PermissionCodes.FlowiseWorkspacesView,
            PermissionCodes.FlowiseLoginActivityView,
            PermissionCodes.FlowiseLogsView,
            PermissionCodes.FlowiseAccountView);
    }

    public void EnsureAny(params string[] permissionCodes)
    {
        if (permissionCodes.Any(code => currentUser.HasAsterErpPermission(code)))
        {
            return;
        }

        throw new ValidationException("无权限访问 Flowise Studio 资源", ErrorCodes.PermissionDenied);
    }
}
