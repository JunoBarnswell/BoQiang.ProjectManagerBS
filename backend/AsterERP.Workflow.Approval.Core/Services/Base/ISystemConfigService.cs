using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public interface ISystemConfigService
{
    Task<PagerModel<SystemConfig>> GetPagerModelByWrapperAsync(SystemConfig systemConfig, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAsync(SystemConfig systemConfig, User loginUser, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> DeleteByIdsAsync(string[] ids, CancellationToken cancellationToken = default);
}
