using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface IUserService
{
    Task<PagerModel<User>> GetPagerModelByWrapperAsync(User user, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(User user, User loginUser, CancellationToken cancellationToken = default);
    Task SetPasswordAsync(User user, CancellationToken cancellationToken = default);
    Task<ReturnVo<User>> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
    Task DeleteByIdsAsync(List<string> userIds, CancellationToken cancellationToken = default);
}
