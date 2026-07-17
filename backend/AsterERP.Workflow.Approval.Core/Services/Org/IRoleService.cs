using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IRoleService
{
    Task<Role?> GetRoleBySnAsync(string sn, CancellationToken cancellationToken = default);
    Task<PagerModel<Role>> GetPagerModelByWrapperAsync(Role role, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(Role role, User loginUser, CancellationToken cancellationToken = default);
}
