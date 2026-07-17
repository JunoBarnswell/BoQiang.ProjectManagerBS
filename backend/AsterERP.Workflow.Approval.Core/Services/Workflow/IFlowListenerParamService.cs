using AsterERP.Workflow.Approval.Api.Models.Workflow;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IFlowListenerParamService
{
    Task<List<FlowListenerParam>> GetListByListenerIdAsync(string listenerId, CancellationToken cancellationToken = default);
}
