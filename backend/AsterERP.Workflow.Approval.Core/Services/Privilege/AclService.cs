using System.Numerics;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Api.ViewModels.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using SqlSugar;
using Volo.Abp.Guids;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class AclService : IAclService
{
    private readonly IAclRepository _aclRepository;
    private readonly IAppPrivilegeValueService _appPrivilegeValueService;
    private readonly IGroupService _groupService;
    private readonly IModuleService _moduleService;
    private readonly IGuidGenerator _guidGenerator;

    public AclService(
        IAclRepository aclRepository,
        IAppPrivilegeValueService appPrivilegeValueService,
        IGroupService groupService,
        IModuleService moduleService,
        IGuidGenerator guidGenerator)
    {
        _aclRepository = aclRepository;
        _appPrivilegeValueService = appPrivilegeValueService;
        _groupService = groupService;
        _moduleService = moduleService;
        _guidGenerator = guidGenerator;
    }

    public async Task<List<Module>> GetModuleAclsByGroupIdsAsync(List<string> groupIds, CancellationToken cancellationToken = default)
    {
        var spvs = await _appPrivilegeValueService.GetAppPrivilegeValuesAsync(cancellationToken);
        List<Acl> acls = new();
        if (groupIds != null && groupIds.Count > 0)
        {
            acls = await _aclRepository.GetAclsByGroupIdsAsync(groupIds, cancellationToken);
        }
        var datas = await _moduleService.GetActiveModulesAsync(cancellationToken);
        if (datas != null && datas.Count > 0)
        {
            foreach (var module in datas)
            {
                var msvs = GetModuleSystemPrivilegeValues(spvs, acls, module);
                module.Pvs = msvs;
            }
        }
        return datas;
    }

    private List<AppPrivilegeValue> GetModuleSystemPrivilegeValues(List<AppPrivilegeValue> spvs, List<Acl> acls, Module module)
    {
        var msvs = new List<AppPrivilegeValue>();
        foreach (var spv in spvs)
        {
            var flag = module.GetPermission(spv.Position ?? 0);
            if (flag)
            {
                spv.ModuleId = module.Id;
                spv.Flag = HasPermission(acls, module.Id, spv.Position ?? 0);
                var clObj = new AppPrivilegeValue
                {
                    Position = spv.Position,
                    Name = spv.Name,
                    OrderNo = spv.OrderNo,
                    ModuleId = spv.ModuleId,
                    Flag = spv.Flag
                };
                msvs.Add(clObj);
            }
        }
        return msvs;
    }

    private bool HasPermission(List<Acl> acls, string moduleId, int position)
    {
        if (acls != null && acls.Count > 0)
        {
            foreach (var acl in acls)
            {
                if (acl.ModuleId == moduleId)
                {
                    return acl.GetPermission(position) > 0;
                }
            }
        }
        return false;
    }

    public async Task<HashSet<Acl>> GetAclsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var groups = await _groupService.GetGroupsByUserIdAsync(userId, cancellationToken);
        var acls = new HashSet<Acl>();
        var moduleAcls = new Dictionary<string, Acl>();

        if (groups != null && groups.Count > 0)
        {
            var groupIds = groups.Select(g => g.Id).ToList();
            var roleAcls = await _aclRepository.GetAclsByGroupIdsAsync(groupIds, cancellationToken);
            if (roleAcls != null && roleAcls.Count > 0)
            {
                foreach (var acl in roleAcls)
                {
                    var moduleId = acl.ModuleId;
                    if (moduleAcls.ContainsKey(moduleId))
                    {
                        var mAcl = moduleAcls[moduleId];
                        var mAclState = mAcl.GetAclStateValue();
                        var aAclState = acl.GetAclStateValue();
                        mAcl.SetAclState(mAclState | aAclState);
                        moduleAcls[moduleId] = mAcl;
                    }
                    else
                    {
                        moduleAcls[moduleId] = acl;
                    }
                }
            }
            foreach (var kvp in moduleAcls)
            {
                acls.Add(kvp.Value);
            }
        }
        return acls;
    }

    public async Task<HashSet<ModulePermission>> GetModulePermissionsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var mps = new HashSet<ModulePermission>();
        var acls = await GetAclsByUserIdAsync(userId, cancellationToken);
        if (acls == null || acls.Count == 0) return mps;

        var appPrivilegeValues = await _appPrivilegeValueService.GetAppPrivilegeValuesAsync(cancellationToken);
        foreach (var acl in acls)
        {
            if (appPrivilegeValues != null && appPrivilegeValues.Count > 0)
            {
                foreach (var sv in appPrivilegeValues)
                {
                    if (acl.GetPermission(sv.Position ?? 0) > 0)
                    {
                        mps.Add(new ModulePermission { ModuleSn = acl.ModuleSn, PermissionName = sv.Name, PermissionValue = sv.Position ?? 0 });
                    }
                }
            }
        }
        return mps;
    }

    public async Task<HashSet<ModulePermission>> GetModulePermissionsByAdminIdAsync(string admin, CancellationToken cancellationToken = default)
    {
        var moduleList = await _moduleService.GetActiveModulesAsync(cancellationToken);
        var mps = new HashSet<ModulePermission>();
        var appPrivilegeValues = await _appPrivilegeValueService.GetAppPrivilegeValuesAsync(cancellationToken);
        foreach (var module in moduleList)
        {
            if (appPrivilegeValues != null && appPrivilegeValues.Count > 0)
            {
                foreach (var apv in appPrivilegeValues)
                {
                    mps.Add(new ModulePermission { ModuleSn = module.Sn, PermissionName = apv.Name, PermissionValue = apv.Position ?? 0 });
                }
            }
        }
        return mps;
    }

    public async Task CreateAclByModuleAsync(Acl acl, bool yes, CancellationToken cancellationToken = default)
    {
        if (yes)
        {
            var currAcl = await _aclRepository.Db.Queryable<Acl>()
                .FirstAsync(a => a.ModuleId == acl.ModuleId && a.ReleaseId == acl.ReleaseId, cancellationToken);
            var module = await _moduleService.GetByIdAsync(acl.ModuleId!, cancellationToken);
            if (currAcl != null)
            {
                currAcl.SetAclState(module?.GetStateValue() ?? BigInteger.Zero);
                await _aclRepository.UpdateAsync(currAcl, cancellationToken);
            }
            else
            {
                var al = new Acl
                {
                    Id = _guidGenerator.Create().ToString("N"),
                    ModuleId = module?.Id,
                    ModuleSn = module?.Sn,
                    ReleaseId = acl.ReleaseId
                };
                al.SetAclState(module?.GetStateValue() ?? BigInteger.Zero);
                await _aclRepository.InsertAsync(al, cancellationToken);
            }
        }
        else
        {
            await _aclRepository.Db.Deleteable<Acl>()
                .Where(a => a.ReleaseId == acl.ReleaseId && a.ModuleId == acl.ModuleId)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task CreateAllAclAsync(Acl acl, bool yes, CancellationToken cancellationToken = default)
    {
        await _aclRepository.Db.Deleteable<Acl>()
            .WhereIF(!string.IsNullOrWhiteSpace(acl.ReleaseId), a => a.ReleaseId == acl.ReleaseId)
            .ExecuteCommandAsync(cancellationToken);
        if (yes)
        {
            var modules = await _moduleService.GetActiveModulesAsync(cancellationToken);
            if (modules.Count == 0)
            {
                return;
            }

            var createdAcls = new List<Acl>(modules.Count);
            foreach (var m in modules)
            {
                var createdAcl = new Acl
                {
                    Id = _guidGenerator.Create().ToString("N"),
                    ModuleId = m.Id,
                    ModuleSn = m.Sn,
                    ReleaseId = acl.ReleaseId
                };
                createdAcl.SetAclState(m.GetStateValue());
                createdAcls.Add(createdAcl);
            }

            await _aclRepository.Db.Insertable(createdAcls).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task CreateAclAsync(Acl acl, int position, bool yes, CancellationToken cancellationToken = default)
    {
        var currAcl = await _aclRepository.Db.Queryable<Acl>()
            .FirstAsync(a => a.ModuleId == acl.ModuleId && a.ReleaseId == acl.ReleaseId, cancellationToken);
        if (currAcl != null)
        {
            currAcl.SetPermission(position, yes);
            var aclState = currAcl.AclState ?? "";
            if (!string.IsNullOrWhiteSpace(aclState) && aclState != "0")
            {
                await _aclRepository.UpdateAsync(currAcl, cancellationToken);
            }
            else
            {
                await _aclRepository.DeleteAsync(currAcl.Id, cancellationToken);
            }
        }
        else
        {
            var module = await _moduleService.GetByIdAsync(acl.ModuleId!, cancellationToken);
            acl.ModuleSn = module?.Sn;
            acl.Id = _guidGenerator.Create().ToString("N");
            acl.SetPermission(position, yes);
            await _aclRepository.InsertAsync(acl, cancellationToken);
        }
    }

    public async Task SetAclModuleListAsync(List<int> positions, string moduleId, string groupId, CancellationToken cancellationToken = default)
    {
        await _aclRepository.Db.Deleteable<Acl>()
            .Where(a => a.ModuleId == moduleId && a.ReleaseId == groupId)
            .ExecuteCommandAsync(cancellationToken);

        if (positions != null && positions.Count > 0)
        {
            var acl = new Acl
            {
                Id = _guidGenerator.Create().ToString("N"),
                ReleaseId = groupId,
                ModuleId = moduleId
            };
            var module = await _moduleService.GetByIdAsync(moduleId, cancellationToken);
            if (module != null)
            {
                acl.ModuleSn = module.Sn;
            }
            foreach (var position in positions)
            {
                acl.SetPermission(position, true);
            }
            await _aclRepository.InsertAsync(acl, cancellationToken);
        }
    }

    public async Task<Dictionary<string, List<int>>> GetAllPermissionsAsync(string username, string userId, CancellationToken cancellationToken = default)
    {
        var allPermissionMap = new Dictionary<string, List<int>>();
        HashSet<ModulePermission> modulePermissions;
        if (username == "admin")
        {
            modulePermissions = await GetModulePermissionsByAdminIdAsync(userId, cancellationToken);
        }
        else
        {
            modulePermissions = await GetModulePermissionsByUserIdAsync(userId, cancellationToken);
        }
        foreach (var mp in modulePermissions)
        {
            if (!allPermissionMap.ContainsKey(mp.ModuleSn))
            {
                allPermissionMap[mp.ModuleSn] = new List<int> { mp.PermissionValue };
            }
            else
            {
                allPermissionMap[mp.ModuleSn].Add(mp.PermissionValue);
            }
        }
        return allPermissionMap;
    }
}
