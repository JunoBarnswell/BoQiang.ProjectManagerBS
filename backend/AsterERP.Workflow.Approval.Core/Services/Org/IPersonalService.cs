using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Org;

public interface IPersonalService
{
    Task<ReturnVo<Personal>> ImportPersonalsAsync(List<Personal> personals, User loginUser, CancellationToken cancellationToken = default);
    Task<PagerModel<Personal>> GetPagerModelByWrapperAsync(Personal personal, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<PagerModel<Personal>> GetPagerModelByWrapperAsync(Personal personal, int pageNum, int pageSize, bool showRoles, CancellationToken cancellationToken = default);
    Task<List<Personal>> GetPersonalsByCodesAsync(List<string> codes, CancellationToken cancellationToken = default);
    Task<List<Personal>> GetPersonalsByRoleIdsAsync(List<string> roleIds, CancellationToken cancellationToken = default);
    Task<List<Personal>> GetPersonalsByRoleSnsAsync(List<string> roleSns, CancellationToken cancellationToken = default);
    Task<Personal?> GetPersonalByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task DeleteByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(Personal personal, User loginUser, CancellationToken cancellationToken = default);
    Task<Personal?> GetPersonalByThirdUserIdAsync(string thirdUserId, CancellationToken cancellationToken = default);
}
