namespace AsterERP.Workflow.Core.Security;

public interface IWorkflowEngineIdentityService
{
    global::System.Threading.Tasks.Task<string> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<string?> GetUserIdAsync(string token, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<string?> GetUserRoleAsync(string userId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<List<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> IsUserInRoleAsync(string userId, string role, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default);
}
