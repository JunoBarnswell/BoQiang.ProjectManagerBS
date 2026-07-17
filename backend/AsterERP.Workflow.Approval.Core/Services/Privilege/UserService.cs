using AsterERP.Workflow.Approval.Api.Constants;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using AsterERP.Workflow.Approval.Core.Repositories.Privilege;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using SqlSugar;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public UserService(
        IUserRepository userRepository,
        IClock clock,
        IGuidGenerator guidGenerator)
    {
        _userRepository = userRepository;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<PagerModel<User>> GetPagerModelByWrapperAsync(User user, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var query = _userRepository.Db.Queryable<User>()
            .Where(u => u.DelFlag == 1);
        if (!string.IsNullOrWhiteSpace(user.Keyword))
        {
            var keyword = user.Keyword.Trim();
            query = query.Where(u => u.UserNo.Contains(keyword) || u.Username.Contains(keyword) || u.Email.Contains(keyword) || u.Mobile.Contains(keyword));
        }
        var list = await query.ToPageListAsync(pageNum, pageSize, total, cancellationToken);

        if (list != null && list.Count > 0)
        {
            var userIds = list.Select(u => u.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
            var groupRows = await _userRepository.Db.Queryable<UserGroup, Group>(
                    (userGroup, group) => new JoinQueryInfos(
                        JoinType.Inner,
                        userGroup.GroupId == group.Id))
                .Where((userGroup, group) => userIds.Contains(userGroup.UserId) && userGroup.DelFlag == 1 && group.DelFlag == 1)
                .Select((userGroup, group) => new
                {
                    userGroup.UserId,
                    Group = group
                })
                .ToListAsync(cancellationToken);

            var groupsByUserId = groupRows
                .GroupBy(item => item.UserId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Group)
                        .Where(item => item != null)
                        .DistinctBy(item => item.Id, StringComparer.Ordinal)
                        .ToList(),
                    StringComparer.Ordinal);

            foreach (var u in list)
            {
                if (!groupsByUserId.TryGetValue(u.Id, out var currentGroups) || currentGroups.Count == 0)
                {
                    u.Groups = [];
                    continue;
                }

                u.Groups = currentGroups;
            }
        }
        return new PagerModel<User>(total.Value, list);
    }

    public async Task SaveOrUpdateAsync(User user, User loginUser, CancellationToken cancellationToken = default)
    {
        var exists = !string.IsNullOrWhiteSpace(user.Id)
            && await _userRepository.GetByIdAsync(user.Id, cancellationToken) != null;

        if (exists)
        {
            user.UpdateTime = _clock.Now;
            user.Updator = loginUser.UserNo;
            user.Keyword ??= string.Empty;
        }
        else
        {
            user.Id = string.IsNullOrWhiteSpace(user.Id) ? _guidGenerator.Create().ToString("N") : user.Id;
            user.CreateTime = _clock.Now;
            user.UpdateTime = user.CreateTime;
            user.Creator = string.IsNullOrWhiteSpace(user.Creator) ? loginUser.UserNo : user.Creator;
            user.Updator ??= string.Empty;
            user.Keyword ??= string.Empty;
        }

        if (!exists)
        {
            await _userRepository.InsertAsync(user, cancellationToken);
        }
        else
        {
            await _userRepository.UpdateAsync(user, cancellationToken);
        }
    }

    public async Task SetPasswordAsync(User user, CancellationToken cancellationToken = default)
    {
        await _userRepository.Db.Updateable<User>()
            .SetColumns(u => u.UpdateTime == _clock.Now)
            .SetColumns(u => u.Password == GetMd5Password(user.Password))
            .Where(u => u.Id == user.Id)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task<ReturnVo<User>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            var md5Password = GetMd5Password(password);
            var user = await _userRepository.Db.Queryable<User>()
                .Where(u => u.Password == md5Password)
                .Where(u => u.Username == username.Trim() || u.Tel == username.Trim() || u.UserNo == username.Trim())
                .Where(u => u.DelFlag == 1)
                .FirstAsync(cancellationToken);
            if (user != null)
            {
                return new ReturnVo<User>(ReturnCode.SUCCESS, "OK") { Data = user };
            }
            return new ReturnVo<User>(ReturnCode.FAIL, "账号和密码不正确!");
        }
        return new ReturnVo<User>(ReturnCode.FAIL, "账号和密码不能为空!");
    }

    public async Task DeleteByIdsAsync(List<string> userIds, CancellationToken cancellationToken = default)
    {
        if (userIds != null && userIds.Count > 0)
        {
            await _userRepository.Db.Deleteable<UserGroup>()
                .In(ug => ug.UserId, userIds)
                .ExecuteCommandAsync(cancellationToken);
            await _userRepository.Db.Deleteable<User>()
                .In(u => u.Id, userIds)
                .ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string GetMd5Password(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ValidationException("password不能为空", ErrorCodes.ParameterInvalid);
        }
        using var md5 = System.Security.Cryptography.MD5.Create();
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(WorkflowApprovalConstants.Md5Prefix + password.Trim());
        var hashBytes = md5.ComputeHash(inputBytes);
        return Convert.ToHexString(hashBytes).ToLower();
    }
}
