using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Org;
using AsterERP.Workflow.Tools.Pager;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public RoleService(IRoleRepository roleRepository, IClock clock, IGuidGenerator guidGenerator)
    {
        _roleRepository = roleRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<Role?> GetRoleBySnAsync(string sn, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sn)) return null;
        return await _roleRepository.Db.Queryable<Role>()
            .FirstAsync(r => r.Sn == sn && r.DelFlag == 1, cancellationToken);
    }

    public async Task<PagerModel<Role>> GetPagerModelByWrapperAsync(Role role, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _roleRepository.Db.Queryable<Role>()
            .WhereIF(!string.IsNullOrWhiteSpace(role.Keyword), r => r.Name.Contains(role.Keyword) || r.Sn.Contains(role.Keyword))
            .Where(r => r.DelFlag == 1)
            .OrderBy(r => r.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<Role>(total.Value, list);
    }

    public async Task DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        await _roleRepository.Db.Deleteable<PersonalRole>()
            .In(pr => pr.RoleId, ids)
            .ExecuteCommandAsync(cancellationToken);
        await _roleRepository.Db.Updateable<Role>()
            .SetColumns(r => r.DelFlag == 0)
            .Where(r => ids.Contains(r.Id))
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task SaveOrUpdateAsync(Role role, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(role.Id)
            && await _roleRepository.GetByIdAsync(role.Id, cancellationToken) != null;

        if (exists)
        {
            role.UpdateTime = _clock.Now;
            role.Updator = loginUser.UserNo;
            role.Keyword ??= string.Empty;
        }
        else
        {
            role.Id = string.IsNullOrWhiteSpace(role.Id) ? _guidGenerator.Create().ToString("N") : role.Id;
            role.CreateTime = _clock.Now;
            role.UpdateTime = role.CreateTime;
            role.Creator = string.IsNullOrWhiteSpace(role.Creator) ? loginUser.UserNo : role.Creator;
            role.Updator ??= string.Empty;
            role.Keyword ??= string.Empty;
        }
        if (!exists)
        {
            await _roleRepository.InsertAsync(role, cancellationToken);
        }
        else
        {
            await _roleRepository.UpdateAsync(role, cancellationToken);
        }
    }
}
