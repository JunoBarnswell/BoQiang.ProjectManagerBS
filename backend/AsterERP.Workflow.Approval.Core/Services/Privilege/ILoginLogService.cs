using AsterERP.Workflow.Approval.Api.Models.Privilege;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Core.Services.Privilege;

public interface ILoginLogService
{
    Task<PagerModel<LoginLog>> GetPagerModelByWrapperAsync(LoginLog loginLog, int pageNum, int pageSize, CancellationToken cancellationToken = default);
}
