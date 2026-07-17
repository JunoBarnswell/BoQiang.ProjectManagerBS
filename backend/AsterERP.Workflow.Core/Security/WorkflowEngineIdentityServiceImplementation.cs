using System.Linq;

namespace AsterERP.Workflow.Core.Security;

public class WorkflowEngineIdentityServiceImplementation : IWorkflowEngineIdentityService
{
    private readonly Dictionary<string, WorkflowUser> _users = new();
    private readonly Dictionary<string, string> _tokens = new();
    private readonly Dictionary<string, List<string>> _userRoles = new();
    private readonly Dictionary<string, List<string>> _userPermissions = new();
    private readonly Dictionary<string, string> _passwords = new();
    private readonly Dictionary<string, string> _usernameToId = new();
    private readonly Dictionary<string, List<string>> _userGroups = new();
    private int _tokenCounter;

    public void RegisterUser(WorkflowUser user, string password, List<string> roles, List<string> permissions)
    {
        _users[user.Id] = user;
        _passwords[user.Id] = password;
        _usernameToId[user.Username] = user.Id;
        _userRoles[user.Id] = roles;
        _userPermissions[user.Id] = permissions;
    }

    public Task<string> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (!_usernameToId.TryGetValue(username, out var userId))
            throw new UnauthorizedAccessException("Invalid username or password");

        if (!_passwords.TryGetValue(userId, out var storedPassword) || storedPassword != password)
            throw new UnauthorizedAccessException("Invalid username or password");

        if (!_users.TryGetValue(userId, out var user) || !user.IsActive)
            throw new UnauthorizedAccessException("User account is inactive");

        var token = $"token-{++_tokenCounter}-{userId}";
        _tokens[token] = userId;

        return Task.FromResult(token);
    }

    public Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_tokens.ContainsKey(token));
    }

    public Task<string?> GetUserIdAsync(string token, CancellationToken cancellationToken = default)
    {
        _tokens.TryGetValue(token, out var userId);
        return Task.FromResult(userId);
    }

    public Task<string?> GetUserRoleAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (_userRoles.TryGetValue(userId, out var roles) && roles.Count > 0)
            return Task.FromResult<string?>(roles[0]);
        return Task.FromResult<string?>(null);
    }

    public Task<List<string>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        _userRoles.TryGetValue(userId, out var roles);
        return Task.FromResult(roles ?? new List<string>());
    }

    public Task<List<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        _userPermissions.TryGetValue(userId, out var permissions);
        return Task.FromResult(permissions ?? new List<string>());
    }

    public Task<bool> IsUserInRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        if (_userRoles.TryGetValue(userId, out var roles))
            return Task.FromResult(roles.Contains(role));
        return Task.FromResult(false);
    }

    public Task<bool> HasPermissionAsync(string userId, string permission, CancellationToken cancellationToken = default)
    {
        if (_userRoles.TryGetValue(userId, out var roles) && roles.Contains(SecurityConstants.RoleAdmin))
            return Task.FromResult(true);

        if (_userPermissions.TryGetValue(userId, out var permissions))
            return Task.FromResult(permissions.Contains(permission));
        return Task.FromResult(false);
    }

    internal WorkflowUser? GetUserById(string userId)
    {
        _users.TryGetValue(userId, out var user);
        return user;
    }

    internal WorkflowUser? GetUserByUsername(string username)
    {
        if (_usernameToId.TryGetValue(username, out var userId))
            return GetUserById(userId);
        return null;
    }

    internal List<WorkflowUser> GetUsersByGroup(string groupId)
    {
        return _users.Values.Where(u => _userGroups.GetValueOrDefault(u.Id)?.Contains(groupId) == true).ToList();
    }

    internal List<string> GetUserGroupIds(string userId)
    {
        _userGroups.TryGetValue(userId, out var groups);
        return groups ?? new List<string>();
    }

    internal bool CheckPassword(string userId, string password)
    {
        return _passwords.TryGetValue(userId, out var storedPassword) && storedPassword == password;
    }

    public void RegisterUserGroup(string userId, string groupId)
    {
        if (!_userGroups.ContainsKey(userId))
            _userGroups[userId] = new List<string>();
        if (!_userGroups[userId].Contains(groupId))
            _userGroups[userId].Add(groupId);
    }
}
