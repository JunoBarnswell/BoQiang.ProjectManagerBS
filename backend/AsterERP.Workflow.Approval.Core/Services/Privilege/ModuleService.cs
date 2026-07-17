using System.Numerics;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class ModuleService : IModuleService
{
    private readonly IModuleRepository _moduleRepository;
    private readonly IAppPrivilegeValueService _appPrivilegeValueService;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public ModuleService(
        IModuleRepository moduleRepository,
        IAppPrivilegeValueService appPrivilegeValueService,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _moduleRepository = moduleRepository;
        _appPrivilegeValueService = appPrivilegeValueService;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task SaveOrUpdateModuleAsync(Module module, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(module.Id)
            && await _moduleRepository.GetByIdAsync(module.Id, cancellationToken) != null;

        if (!exists && string.IsNullOrWhiteSpace(module.Id))
        {
            module.Id = _guidGenerator.Create().ToString("N");
            module.SetState(BigInteger.Zero);
            var positions = new List<int> { 0, 1, 2, 3 };
            foreach (var position in positions)
            {
                module.SetPermission(position, true);
            }
        }

        if (!exists)
        {
            module.CreateTime ??= _clock.Now;
            module.UpdateTime ??= module.CreateTime;
            module.Creator ??= string.Empty;
            module.Updator ??= string.Empty;
            module.Keyword ??= string.Empty;
            await _moduleRepository.InsertAsync(module, cancellationToken);
        }
        else
        {
            module.UpdateTime = _clock.Now;
            module.Updator ??= string.Empty;
            module.Keyword ??= string.Empty;
            await _moduleRepository.UpdateAsync(module, cancellationToken);
        }
    }

    public async Task DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        if (ids != null && ids.Count > 0)
        {
            await _moduleRepository.Db.Deleteable<Acl>()
                .In(a => a.ModuleId, ids)
                .ExecuteCommandAsync(cancellationToken);
            await _moduleRepository.Db.Deleteable<Module>()
                .In(m => m.Id, ids)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task DeletePriValAsync(string appPrivilegeValueId, string moduleId, CancellationToken cancellationToken = default)
    {
        var appPrivilegeValues = await _moduleRepository.Db.Queryable<AppPrivilegeValue>()
            .FirstAsync(a => a.Id == appPrivilegeValueId, cancellationToken);
        var module = await _moduleRepository.GetByIdAsync(moduleId, cancellationToken);
        if (module != null && appPrivilegeValues != null)
        {
            module.SetPermission(appPrivilegeValues.Position ?? 0, false);
            await _moduleRepository.UpdateAsync(module, cancellationToken);

            var acls = await _moduleRepository.Db.Queryable<Acl>()
                .Where(a => a.ModuleId == moduleId)
                .ToListAsync(cancellationToken);
            if (acls.Count > 0)
            {
                foreach (var acl in acls)
                {
                    acl.SetPermission(appPrivilegeValues.Position ?? 0, false);
                }

                await _moduleRepository.Db.Updateable(acls).ExecuteCommandAsync(cancellationToken);
            }
        }
    }

    public async Task AddPriValAsync(List<int> positions, string moduleId, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetByIdAsync(moduleId, cancellationToken);
        if (module != null && positions != null && positions.Count > 0)
        {
            module.SetState(BigInteger.Zero);
            foreach (var position in positions)
            {
                module.SetPermission(position, true);
            }
            await _moduleRepository.UpdateAsync(module, cancellationToken);
        }
    }

    public async Task<List<Module>> GetModulesAsync(Module module, CancellationToken cancellationToken = default)
    {
        var query = _moduleRepository.Db.Queryable<Module>();
        if (!string.IsNullOrWhiteSpace(module.Keyword))
        {
            query = query.Where(m => m.Name.Contains(module.Keyword) || m.Sn.Contains(module.Keyword));
        }
        var modules = await query.Where(m => m.Status == 1 && m.DelFlag == 1)
            .OrderBy(m => m.OrderNo)
            .ToListAsync(cancellationToken);

        var appPrivilegeValues = await _moduleRepository.Db.Queryable<AppPrivilegeValue>()
            .OrderBy(a => a.OrderNo)
            .ToListAsync(cancellationToken);

        foreach (var m in modules)
        {
            var mapvs = new List<AppPrivilegeValue>();
            foreach (var apv in appPrivilegeValues)
            {
                var yes = apv.Position != null && m.GetPermission(apv.Position ?? 0);
                if (yes)
                {
                    mapvs.Add(new AppPrivilegeValue
                    {
                        Position = apv.Position,
                        Name = apv.Name,
                        OrderNo = apv.OrderNo,
                        ModuleId = m.Id
                    });
                }
            }
            m.Pvs = mapvs;
        }
        return modules;
    }

    public async Task<List<Module>> GetActiveModulesAsync(CancellationToken cancellationToken = default)
    {
        return await GetModulesAsync(new Module(), cancellationToken);
    }

    public async Task<List<Module>> GetModulesByIdsAsync(List<string> moduleIds, CancellationToken cancellationToken = default)
    {
        return (await _moduleRepository.GetModulesByIdsAsync(moduleIds, cancellationToken))
            .Where(module => module.DelFlag == 1)
            .ToList();
    }

    public async Task<Module?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _moduleRepository.Db.Queryable<Module>()
            .FirstAsync(module => module.Id == id && module.DelFlag == 1, cancellationToken);
    }

    public async Task<Module?> GetModuleBySnAsync(string sn, CancellationToken cancellationToken = default)
    {
        return await _moduleRepository.Db.Queryable<Module>()
            .FirstAsync(m => m.Sn == sn && m.DelFlag == 1, cancellationToken);
    }
}
