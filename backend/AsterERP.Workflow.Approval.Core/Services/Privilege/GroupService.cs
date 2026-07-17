using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using AsterERP.Workflow.Tools.Pager;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class GroupService : IGroupService
{
    private readonly IGroupRepository _groupRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public GroupService(IGroupRepository groupRepository, IClock clock, IGuidGenerator guidGenerator)
    {
        _groupRepository = groupRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<PagerModel<Group>> GetPagerModelByWrapperAsync(Group group, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _groupRepository.Db.Queryable<Group>()
            .WhereIF(!string.IsNullOrWhiteSpace(group.Keyword), g => g.Name.Contains(group.Keyword) || g.Sn.Contains(group.Keyword))
            .Where(g => g.DelFlag == 1)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<Group>(total.Value, list);
    }

    public async Task<List<Group>> GetGroupsByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return [];
        }

        return await _groupRepository.Db.Queryable<Group>()
            .Where(group => group.DelFlag == 1)
            .Where(group => SqlFunc.Subqueryable<UserGroup>()
                .Where(userGroup => userGroup.DelFlag == 1 && userGroup.UserId == userId && userGroup.GroupId == group.Id)
                .Any())
            .ToListAsync(cancellationToken);
    }

    public async Task SaveOrUpdateAsync(Group group, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(group.Id)
            && await _groupRepository.GetByIdAsync(group.Id, cancellationToken) != null;

        if (exists)
        {
            group.UpdateTime = _clock.Now;
            group.Updator = loginUser.UserNo;
            group.Keyword ??= string.Empty;
        }
        else
        {
            group.Id = string.IsNullOrWhiteSpace(group.Id) ? _guidGenerator.Create().ToString("N") : group.Id;
            group.CreateTime = _clock.Now;
            group.UpdateTime = group.CreateTime;
            group.Creator = string.IsNullOrWhiteSpace(group.Creator) ? loginUser.UserNo : group.Creator;
            group.Updator ??= string.Empty;
            group.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _groupRepository.InsertAsync(group, cancellationToken);
        }
        else
        {
            await _groupRepository.UpdateAsync(group, cancellationToken);
        }
    }

    public async Task DeleteByIdsAsync(List<string> groupIds, CancellationToken cancellationToken = default)
    {
        if (groupIds != null && groupIds.Count > 0)
        {
            await _groupRepository.Db.Deleteable<Acl>()
                .In(a => a.ReleaseId, groupIds)
                .ExecuteCommandAsync(cancellationToken);
            await _groupRepository.Db.Deleteable<UserGroup>()
                .In(ug => ug.GroupId, groupIds)
                .ExecuteCommandAsync(cancellationToken);
            await _groupRepository.Db.Deleteable<Group>()
                .In(g => g.Id, groupIds)
                .ExecuteCommandAsync(cancellationToken);
        }
    }
}
