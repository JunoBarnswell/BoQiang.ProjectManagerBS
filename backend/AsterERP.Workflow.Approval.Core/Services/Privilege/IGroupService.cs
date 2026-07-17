using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface IGroupService
{
    Task<PagerModel<Group>> GetPagerModelByWrapperAsync(Group group, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<List<Group>> GetGroupsByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(Group group, User loginUser, CancellationToken cancellationToken = default);
    Task DeleteByIdsAsync(List<string> groupIds, CancellationToken cancellationToken = default);
}
