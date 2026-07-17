namespace AsterERP.Workflow.Core.Security;

public class AccessControlServiceImplementation : IAccessControlService
{
    private readonly IWorkflowEngineIdentityService _identityService;

    public AccessControlServiceImplementation(IWorkflowEngineIdentityService identityService)
    {
        _identityService = identityService;
    }

    public async Task<bool> CanStartProcessAsync(string userId, string processDefinitionId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionCreateProcessInstance, cancellationToken);
    }

    public async Task<bool> CanClaimTaskAsync(string userId, string taskId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionUpdateTask, cancellationToken);
    }

    public async Task<bool> CanCompleteTaskAsync(string userId, string taskId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionUpdateTask, cancellationToken);
    }

    public async Task<bool> CanViewProcessInstanceAsync(string userId, string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionReadProcessDefinition, cancellationToken);
    }

    public async Task<bool> CanDeleteProcessInstanceAsync(string userId, string processInstanceId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionAdmin, cancellationToken);
    }

    public async Task<bool> CanViewTaskAsync(string userId, string taskId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionReadTask, cancellationToken);
    }

    public async Task<bool> CanAdminAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await IsAdminOrHasPermission(userId, SecurityConstants.PermissionAdmin, cancellationToken);
    }

    private async Task<bool> IsAdminOrHasPermission(string userId, string permission, CancellationToken cancellationToken)
    {
        if (await _identityService.IsUserInRoleAsync(userId, SecurityConstants.RoleAdmin, cancellationToken))
            return true;
        if (await _identityService.IsUserInRoleAsync(userId, SecurityConstants.RoleAppAdmin, cancellationToken))
            return true;
        return await _identityService.HasPermissionAsync(userId, permission, cancellationToken);
    }
}

