using AsterERP.Api.Modules.Platform;
using AsterERP.Api.Modules.System.Organizations;
using AsterERP.Api.Modules.System.Roles;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Workflow.Persistence.Entities;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Workflows;

public sealed class WorkflowIdentitySyncService(ISqlSugarClient db, ILogger<WorkflowIdentitySyncService> logger)
{
    private const int PageSize = 200;

    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var users = await SyncUsersAsync(cancellationToken);
        var groups = await SyncGroupsAsync(cancellationToken);
        var memberships = 0;

        memberships += await SyncUserRoleMembershipsAsync(cancellationToken);
        memberships += await SyncUserAppRoleMembershipsAsync(cancellationToken);
        memberships += await SyncDepartmentMembershipsAsync(cancellationToken);
        memberships += await SyncPositionMembershipsAsync(cancellationToken);

        logger.LogInformation(
            "Workflow identity sync completed. Users={UserCount}, Groups={GroupCount}, Memberships={MembershipCount}",
            users,
            groups,
            memberships);
    }

    public Task SyncAsync(ISqlSugarClient targetDb, CancellationToken cancellationToken = default)
    {
        var scopedSync = new WorkflowIdentitySyncService(targetDb, logger);
        return scopedSync.SyncAsync(cancellationToken);
    }

    private async Task<int> SyncUsersAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var users = await db.Queryable<SystemUserEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .OrderBy(item => item.Id)
                .Select(item => new ActIdUserEntity
                {
                    Id = item.Id,
                    Revision = 1,
                    DisplayName = item.DisplayName,
                    FirstName = item.DisplayName,
                    LastName = item.UserName,
                    Email = item.Email,
                    Password = null,
                    TenantId = null
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (users.Count == 0)
            {
                return totalSynced;
            }

            await db.Storageable(users).ExecuteCommandAsync(cancellationToken);
            totalSynced += users.Count;

            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncGroupsAsync(CancellationToken cancellationToken)
    {
        var synced = 0;
        synced += await SyncRoleGroupsAsync(cancellationToken);
        synced += await SyncDepartmentGroupsAsync(cancellationToken);
        synced += await SyncPositionGroupsAsync(cancellationToken);
        return synced;
    }

    private async Task<int> SyncRoleGroupsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemRoleEntity>()
                .Where(item => !item.IsDeleted && item.IsEnabled)
                .OrderBy(item => item.Id)
                .Select(item => new WorkflowIdentityGroupRow
                {
                    Name = item.RoleName,
                    SourceId = item.Id,
                    Type = "role"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            var groups = rows
                .Select(row => new ActIdGroupEntity
                {
                    Id = WorkflowIdentityKeys.RoleGroup(row.SourceId),
                    Revision = 1,
                    Name = row.Name,
                    Type = row.Type
                })
                .ToList();
            await db.Storageable(groups).ExecuteCommandAsync(cancellationToken);
            totalSynced += groups.Count;

            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncDepartmentGroupsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemDepartmentEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .OrderBy(item => item.Id)
                .Select(item => new WorkflowIdentityGroupRow
                {
                    Name = item.DeptName,
                    SourceId = item.Id,
                    Type = "department"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            var groups = rows
                .Select(row => new ActIdGroupEntity
                {
                    Id = WorkflowIdentityKeys.DepartmentGroup(row.SourceId),
                    Revision = 1,
                    Name = row.Name,
                    Type = row.Type
                })
                .ToList();
            await db.Storageable(groups).ExecuteCommandAsync(cancellationToken);
            totalSynced += groups.Count;

            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncPositionGroupsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemPositionEntity>()
                .Where(item => !item.IsDeleted && item.Status == "Enabled")
                .OrderBy(item => item.Id)
                .Select(item => new WorkflowIdentityGroupRow
                {
                    Name = item.PositionName,
                    SourceId = item.Id,
                    Type = "position"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            var groups = rows
                .Select(row => new ActIdGroupEntity
                {
                    Id = WorkflowIdentityKeys.PositionGroup(row.SourceId),
                    Revision = 1,
                    Name = row.Name,
                    Type = row.Type
                })
                .ToList();
            await db.Storageable(groups).ExecuteCommandAsync(cancellationToken);
            totalSynced += groups.Count;

            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncUserRoleMembershipsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemUserRoleEntity, SystemUserEntity, SystemRoleEntity>(
                    (relation, user, role) => relation.UserId == user.Id && relation.RoleId == role.Id)
                .Where((relation, user, role) => !relation.IsDeleted && !user.IsDeleted && user.Status == "Enabled" && !role.IsDeleted && role.IsEnabled)
                .OrderBy((relation, user, role) => relation.Id)
                .Select((relation, user, role) => new WorkflowIdentityMembershipRow
                {
                    UserId = relation.UserId,
                    GroupId = relation.RoleId,
                    GroupKind = "role"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            totalSynced += await StoreMembershipsAsync(rows, cancellationToken);
            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncUserAppRoleMembershipsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemUserAppRoleEntity, SystemUserEntity, SystemRoleEntity>(
                    (relation, user, role) => relation.UserId == user.Id && relation.RoleId == role.Id)
                .Where((relation, user, role) => !relation.IsDeleted && !user.IsDeleted && user.Status == "Enabled" && !role.IsDeleted && role.IsEnabled)
                .OrderBy((relation, user, role) => relation.Id)
                .Select((relation, user, role) => new WorkflowIdentityMembershipRow
                {
                    UserId = relation.UserId,
                    GroupId = relation.RoleId,
                    GroupKind = "role"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            totalSynced += await StoreMembershipsAsync(rows, cancellationToken);
            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncDepartmentMembershipsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemUserEmploymentEntity, SystemUserEntity, SystemDepartmentEntity>(
                    (employment, user, department) => employment.UserId == user.Id && employment.DeptId == department.Id)
                .Where((employment, user, department) =>
                    !employment.IsDeleted &&
                    employment.Status == "Enabled" &&
                    !user.IsDeleted &&
                    user.Status == "Enabled" &&
                    !department.IsDeleted &&
                    department.Status == "Enabled")
                .OrderBy((employment, user, department) => employment.Id)
                .Select((employment, user, department) => new WorkflowIdentityMembershipRow
                {
                    UserId = user.Id,
                    GroupId = department.Id,
                    GroupKind = "department"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            totalSynced += await StoreMembershipsAsync(rows, cancellationToken);
            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> SyncPositionMembershipsAsync(CancellationToken cancellationToken)
    {
        var totalSynced = 0;
        var pageIndex = 1;

        while (true)
        {
            var total = new RefAsync<int>();
            var rows = await db.Queryable<SystemUserEmploymentEntity, SystemUserEntity, SystemPositionEntity>(
                    (employment, user, position) => employment.UserId == user.Id && employment.PositionId == position.Id)
                .Where((employment, user, position) =>
                    !employment.IsDeleted &&
                    employment.Status == "Enabled" &&
                    !user.IsDeleted &&
                    user.Status == "Enabled" &&
                    !position.IsDeleted &&
                    position.Status == "Enabled")
                .OrderBy((employment, user, position) => employment.Id)
                .Select((employment, user, position) => new WorkflowIdentityMembershipRow
                {
                    UserId = user.Id,
                    GroupId = position.Id,
                    GroupKind = "position"
                })
                .ToPageListAsync(pageIndex, PageSize, total, cancellationToken);

            if (rows.Count == 0)
            {
                return totalSynced;
            }

            totalSynced += await StoreMembershipsAsync(rows, cancellationToken);
            if (pageIndex * PageSize >= total.Value)
            {
                return totalSynced;
            }

            pageIndex++;
        }
    }

    private async Task<int> StoreMembershipsAsync(
        IReadOnlyCollection<WorkflowIdentityMembershipRow> rows,
        CancellationToken cancellationToken)
    {
        var memberships = rows
            .Select(row => BuildMembership(row.UserId, ResolveGroupId(row)))
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        if (memberships.Count == 0)
        {
            return 0;
        }

        var membershipIds = memberships.Select(item => item.Id).ToArray();
        var existingIds = await db.Queryable<ActIdMembershipEntity>()
            .Where(item => membershipIds.Contains(item.Id))
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var existingIdSet = existingIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inserts = memberships
            .Where(item => !existingIdSet.Contains(item.Id))
            .ToList();

        if (inserts.Count == 0)
        {
            return 0;
        }

        await db.Insertable(inserts).ExecuteCommandAsync(cancellationToken);
        return inserts.Count;
    }

    private static string ResolveGroupId(WorkflowIdentityMembershipRow row)
    {
        return row.GroupKind switch
        {
            "department" => WorkflowIdentityKeys.DepartmentGroup(row.GroupId),
            "position" => WorkflowIdentityKeys.PositionGroup(row.GroupId),
            _ => WorkflowIdentityKeys.RoleGroup(row.GroupId)
        };
    }

    private static ActIdMembershipEntity BuildMembership(string userId, string groupId)
    {
        return new ActIdMembershipEntity
        {
            Id = $"{userId}:{groupId}",
            UserId = userId,
            GroupId = groupId
        };
    }

    private sealed class WorkflowIdentityMembershipRow
    {
        public string UserId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string GroupKind { get; set; } = string.Empty;
    }

    private sealed class WorkflowIdentityGroupRow
    {
        public string SourceId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;
    }
}
