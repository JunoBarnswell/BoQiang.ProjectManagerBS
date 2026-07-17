using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Tools.Pager;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IFlowListenerService
{
    Task<List<FlowListener>> GetListAndParamsAsync(FlowListener flowListener, CancellationToken cancellationToken = default);
    Task<List<FlowListener>> GetListAsync(FlowListener flowListener, CancellationToken cancellationToken = default);
    Task<PagerModel<FlowListener>> GetPagerModelAsync(FlowListener flowListener, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default);
}
