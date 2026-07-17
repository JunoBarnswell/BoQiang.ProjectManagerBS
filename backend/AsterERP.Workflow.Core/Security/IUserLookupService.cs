namespace AsterERP.Workflow.Core.Security;

public interface IUserLookupService
{
    global::System.Threading.Tasks.Task<WorkflowUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<WorkflowUser?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<List<WorkflowUser>> GetUsersInGroupAsync(string groupId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<List<string>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> CheckPasswordAsync(string userId, string password, CancellationToken cancellationToken = default);
}
