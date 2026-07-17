using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class FlowListenerParamService : IFlowListenerParamService
{
    private readonly IFlowListenerParamRepository _repository;

    public FlowListenerParamService(IFlowListenerParamRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<FlowListenerParam>> GetListByListenerIdAsync(string listenerId, CancellationToken cancellationToken = default)
    {
        return await _repository.Db.Queryable<FlowListenerParam>()
            .Where(p => p.ListenerId == listenerId)
            .ToListAsync(cancellationToken);
    }
}
