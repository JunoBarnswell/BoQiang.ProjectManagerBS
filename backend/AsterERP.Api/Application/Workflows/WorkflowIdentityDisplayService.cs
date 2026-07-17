using AsterERP.Api.Infrastructure.Workflows;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.Workflows;
using SqlSugar;

using PersistenceActIdUserEntity = AsterERP.Workflow.Persistence.Entities.ActIdUserEntity;

namespace AsterERP.Api.Application.Workflows;

public interface IWorkflowIdentityDisplayService
{
    Task<Dictionary<string, string>> GetUserDisplayNamesAsync(IEnumerable<string?> userIds, CancellationToken cancellationToken);

    Task<Dictionary<string, string>> GetGroupDisplayNamesAsync(IEnumerable<string?> groupIds, CancellationToken cancellationToken);

    string? ResolveCandidateName(
        WorkflowIdentityLinkResponse link,
        IReadOnlyDictionary<string, string> users,
        IReadOnlyDictionary<string, string> groups);

    string? ResolveUserName(string? userId, IReadOnlyDictionary<string, string> users);
}

public sealed class WorkflowIdentityDisplayService(IWorkspaceDatabaseAccessor databaseAccessor) : IWorkflowIdentityDisplayService
{
    public async Task<Dictionary<string, string>> GetUserDisplayNamesAsync(
        IEnumerable<string?> userIds,
        CancellationToken cancellationToken)
    {
        var ids = NormalizeIds(userIds);
        if (ids.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(item => !item.IsDeleted && (ids.Contains(item.Id) || ids.Contains(item.UserName)))
            .ToListAsync(cancellationToken);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var user in users)
        {
            var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
            result[user.Id] = displayName;
            result[user.UserName] = displayName;
        }

        var missingIds = ids.Where(item => !result.ContainsKey(item)).ToList();
        if (missingIds.Count > 0)
        {
            var identityUsers = await databaseAccessor.GetCurrentDb().Queryable<PersistenceActIdUserEntity>()
                .Where(item =>
                    missingIds.Contains(item.Id) ||
                    (item.LastName != null && missingIds.Contains(item.LastName)))
                .ToListAsync(cancellationToken);
            foreach (var identityUser in identityUsers)
            {
                var displayName = !string.IsNullOrWhiteSpace(identityUser.DisplayName)
                    ? identityUser.DisplayName!
                    : !string.IsNullOrWhiteSpace(identityUser.FirstName)
                        ? identityUser.FirstName!
                        : !string.IsNullOrWhiteSpace(identityUser.LastName)
                            ? identityUser.LastName!
                            : identityUser.Id;
                result[identityUser.Id] = displayName;
                if (!string.IsNullOrWhiteSpace(identityUser.LastName))
                {
                    result[identityUser.LastName!] = displayName;
                }
            }
        }

        return result;
    }

    public async Task<Dictionary<string, string>> GetGroupDisplayNamesAsync(IEnumerable<string?> groupIds, CancellationToken cancellationToken)
    {
        var groups = NormalizeIds(groupIds);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var roleIds = groups.Select(group => TryTrimGroupPrefix(group, "role:")).Where(id => id is not null).Select(id => id!).ToList();
        var departmentIds = groups.Select(group => TryTrimGroupPrefix(group, "dept:")).Where(id => id is not null).Select(id => id!).ToList();
        var positionIds = groups.Select(group => TryTrimGroupPrefix(group, "position:")).Where(id => id is not null).Select(id => id!).ToList();

        if (roleIds.Count > 0)
        {
            var roles = await databaseAccessor.GetCurrentDb().Queryable<SystemRoleEntity>()
                .Where(item => !item.IsDeleted && roleIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            foreach (var role in roles)
            {
                result[WorkflowIdentityKeys.RoleGroup(role.Id)] = $"角色:{role.RoleName}";
            }
        }

        if (departmentIds.Count > 0)
        {
            var departments = await databaseAccessor.GetCurrentDb().Queryable<SystemDepartmentEntity>()
                .Where(item => !item.IsDeleted && departmentIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            foreach (var department in departments)
            {
                result[WorkflowIdentityKeys.DepartmentGroup(department.Id)] = $"部门:{department.DeptName}";
            }
        }

        if (positionIds.Count > 0)
        {
            var positions = await databaseAccessor.GetCurrentDb().Queryable<SystemPositionEntity>()
                .Where(item => !item.IsDeleted && positionIds.Contains(item.Id))
                .ToListAsync(cancellationToken);
            foreach (var position in positions)
            {
                result[WorkflowIdentityKeys.PositionGroup(position.Id)] = $"岗位:{position.PositionName}";
            }
        }

        return result;
    }

    public string? ResolveCandidateName(
        WorkflowIdentityLinkResponse link,
        IReadOnlyDictionary<string, string> users,
        IReadOnlyDictionary<string, string> groups)
    {
        if (!string.IsNullOrWhiteSpace(link.UserId))
        {
            return ResolveUserName(link.UserId, users);
        }

        if (!string.IsNullOrWhiteSpace(link.GroupId))
        {
            return groups.TryGetValue(link.GroupId, out var name) ? name : link.GroupId;
        }

        return null;
    }

    public string? ResolveUserName(string? userId, IReadOnlyDictionary<string, string> users)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return users.TryGetValue(userId, out var name) ? name : userId;
    }

    private static IReadOnlyCollection<string> NormalizeIds(IEnumerable<string?> ids) =>
        ids.Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? TryTrimGroupPrefix(string groupId, string prefix)
    {
        return groupId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? groupId[prefix.Length..]
            : null;
    }
}

