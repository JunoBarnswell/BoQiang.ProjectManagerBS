using AsterERP.Workflow.Approval.Api.Models.Base;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Core.Services.Base;

public interface IAppService
{
    Task<List<App>> GetActiveAppsAsync(CancellationToken cancellationToken = default);
    Task<PagerModel<App>> GetPagerModelByWrapperAsync(App app, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task SaveOrUpdateAppAsync(App app, CancellationToken cancellationToken = default);
    Task<string> UpdateSecretKeyAsync(string appId, CancellationToken cancellationToken = default);
}
