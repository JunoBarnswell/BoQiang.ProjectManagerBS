using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using AsterERP.Workflow.Tools.Pager;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class UserGroupService : IUserGroupService
{
    private readonly IUserGroupRepository _userGroupRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public UserGroupService(IUserGroupRepository userGroupRepository, IClock clock, IGuidGenerator guidGenerator)
    {
        _userGroupRepository = userGroupRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<Dictionary<string, List<string>>> GetGroupIdsByUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<string, List<string>>(StringComparer.Ordinal);
        }

        var userGroupRows = await _userGroupRepository.Db.Queryable<UserGroup>()
            .Where(ug => userIds.Contains(ug.UserId) && ug.DelFlag == 1)
            .ToListAsync(cancellationToken);

        return userGroupRows
            .GroupBy(row => row.UserId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(row => row.GroupId).Distinct(StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);
    }

    public async Task<PagerModel<UserGroup>> GetPagerModelByWrapperAsync(UserGroup userGroup, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _userGroupRepository.Db.Queryable<UserGroup>()
            .Where(ug => ug.DelFlag == 1)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<UserGroup>(total.Value, list);
    }

    public async Task AddUserGroupsByGroupAsync(string groupId, List<User> users, User loginUser, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(groupId))
        {
            await _userGroupRepository.Db.Deleteable<UserGroup>()
                .Where(ug => ug.GroupId == groupId)
                .ExecuteCommandAsync(cancellationToken);
        }
        var urs = new List<UserGroup>();
        var g = await _userGroupRepository.Db.Queryable<Group>()
            .FirstAsync(x => x.Id == groupId && x.DelFlag == 1, cancellationToken);
        if (users != null && users.Count > 0 && g != null)
        {
            foreach (var user in users)
            {
                urs.Add(new UserGroup
                {
                    Id = _guidGenerator.Create().ToString("N"),
                    UserId = user.Id,
                    UserNo = user.UserNo,
                    GroupId = g.Id,
                    GroupSn = g.Sn,
                    CreateTime = _clock.Now,
                    Creator = loginUser.UserNo,
                    UpdateTime = _clock.Now,
                    Updator = loginUser.UserNo
                });
            }
            await _userGroupRepository.Db.Insertable(urs).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task AddUserGroupsByUserAsync(string userId, List<Group> groups, User loginUser, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(userId))
        {
            await _userGroupRepository.Db.Deleteable<UserGroup>()
                .Where(ug => ug.UserId == userId)
                .ExecuteCommandAsync(cancellationToken);
        }
        var urs = new List<UserGroup>();
        var user = await _userGroupRepository.Db.Queryable<User>()
            .FirstAsync(u => u.Id == userId && u.DelFlag == 1, cancellationToken);
        if (groups != null && groups.Count > 0 && user != null)
        {
            foreach (var group in groups)
            {
                urs.Add(new UserGroup
                {
                    Id = _guidGenerator.Create().ToString("N"),
                    UserId = userId,
                    UserNo = user.UserNo,
                    GroupId = group.Id,
                    GroupSn = group.Sn,
                    CreateTime = _clock.Now,
                    Creator = loginUser.UserNo,
                    UpdateTime = _clock.Now,
                    Updator = loginUser.UserNo
                });
            }
            await _userGroupRepository.Db.Insertable(urs).ExecuteCommandAsync(cancellationToken);
        }
    }
}
