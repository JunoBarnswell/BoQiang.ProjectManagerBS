using AsterERP.Workflow.Core.Security;

namespace AsterERP.Workflow.Core.Security;

public class UserLookupServiceImplementation : IUserLookupService
{
    private readonly WorkflowEngineIdentityServiceImplementation _identityService;

    public UserLookupServiceImplementation(WorkflowEngineIdentityServiceImplementation identityService)
    {
        _identityService = identityService;
    }

    public Task<WorkflowUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_identityService.GetUserById(userId));
    }

    public Task<WorkflowUser?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_identityService.GetUserByUsername(username));
    }

    public Task<List<WorkflowUser>> GetUsersInGroupAsync(string groupId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_identityService.GetUsersByGroup(groupId));
    }

    public Task<List<string>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_identityService.GetUserGroupIds(userId));
    }

    public Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_identityService.CheckPassword(userId, password));
    }
}

