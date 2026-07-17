using AsterERP.Workflow.Approval.Api.Models.Privilege;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Privilege;

public class AclRepository : SqlSugarRepository<Acl>, IAclRepository
{
    public AclRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<Acl>> GetAclsByGroupIdsAsync(List<string> groupIds, CancellationToken cancellationToken = default)
    {
        if (groupIds == null || groupIds.Count == 0)
        {
            return new List<Acl>();
        }

        return await Db.Queryable<Acl>()
            .In(a => a.ReleaseId, groupIds)
            .ToListAsync(cancellationToken);
    }
}

public class AppPrivilegeValueRepository : SqlSugarRepository<AppPrivilegeValue>, IAppPrivilegeValueRepository
{
    public AppPrivilegeValueRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class GroupRepository : SqlSugarRepository<Group>, IGroupRepository
{
    public GroupRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class LoginLogRepository : SqlSugarRepository<LoginLog>, ILoginLogRepository
{
    public LoginLogRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class ModuleRepository : SqlSugarRepository<Module>, IModuleRepository
{
    public ModuleRepository(ISqlSugarClient db) : base(db)
    {
    }

    public async Task<List<Module>> GetModulesByIdsAsync(List<string> moduleIds, CancellationToken cancellationToken = default)
    {
        if (moduleIds == null || moduleIds.Count == 0)
        {
            return new List<Module>();
        }

        return await Db.Queryable<Module>()
            .In(m => m.Id, moduleIds)
            .ToListAsync(cancellationToken);
    }
}

public class ShiroSessionRepository : SqlSugarRepository<ShiroSession>, IShiroSessionRepository
{
    public ShiroSessionRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class UserRepository : SqlSugarRepository<User>, IUserRepository
{
    public UserRepository(ISqlSugarClient db) : base(db)
    {
    }
}

public class UserGroupRepository : SqlSugarRepository<UserGroup>, IUserGroupRepository
{
    public UserGroupRepository(ISqlSugarClient db) : base(db)
    {
    }
}
