using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface IUserGroupService
{
    Task<Dictionary<string, List<string>>> GetGroupIdsByUserIdsAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default);
    Task<PagerModel<UserGroup>> GetPagerModelByWrapperAsync(UserGroup userGroup, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task AddUserGroupsByGroupAsync(string groupId, List<User> users, User loginUser, CancellationToken cancellationToken = default);
    Task AddUserGroupsByUserAsync(string userId, List<Group> groups, User loginUser, CancellationToken cancellationToken = default);
}
