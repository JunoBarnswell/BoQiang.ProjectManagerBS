using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Tools.Pager;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class FlowListenerService : IFlowListenerService
{
    private readonly IFlowListenerRepository _flowListenerRepository;
    private readonly IFlowListenerParamService _flowListenerParamService;

    public FlowListenerService(
        IFlowListenerRepository flowListenerRepository,
        IFlowListenerParamService flowListenerParamService)
    {
        _flowListenerRepository = flowListenerRepository;
        _flowListenerParamService = flowListenerParamService;
    }

    public async Task<List<FlowListener>> GetListAndParamsAsync(FlowListener flowListener, CancellationToken cancellationToken = default)
    {
        var listeners = await GetListAsync(flowListener, cancellationToken);
        foreach (var listener in listeners)
        {
            listener.FlowListenerParamList = await _flowListenerParamService.GetListByListenerIdAsync(listener.Id, cancellationToken);
        }
        return listeners;
    }

    public async Task<List<FlowListener>> GetListAsync(FlowListener flowListener, CancellationToken cancellationToken = default)
    {
        return await _flowListenerRepository.Db.Queryable<FlowListener>()
            .WhereIF(!string.IsNullOrWhiteSpace(flowListener.ListenerType), f => f.ListenerType == flowListener.ListenerType)
            .WhereIF(!string.IsNullOrWhiteSpace(flowListener.Name), f => f.Name.Contains(flowListener.Name))
            .Where(f => f.DelFlag == 1)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagerModel<FlowListener>> GetPagerModelAsync(FlowListener flowListener, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        RefAsync<int> total = new();
        var list = await _flowListenerRepository.Db.Queryable<FlowListener>()
            .WhereIF(!string.IsNullOrWhiteSpace(flowListener.ListenerType), f => f.ListenerType == flowListener.ListenerType)
            .WhereIF(!string.IsNullOrWhiteSpace(flowListener.Keyword), f => f.Name.Contains(flowListener.Keyword) || f.Value.Contains(flowListener.Keyword) || f.Remark.Contains(flowListener.Keyword))
            .Where(f => f.DelFlag == 1)
            .OrderByDescending(f => f.CreateTime)
            .OrderBy(f => f.OrderNo)
            .ToPageListAsync(pageNum, pageSize, total, cancellationToken);
        return new PagerModel<FlowListener>(total.Value, list);
    }

    public async Task DeleteByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await _flowListenerRepository.Db.Deleteable<FlowListenerParam>()
            .Where(p => p.ListenerId == id)
            .ExecuteCommandAsync(cancellationToken);
        await _flowListenerRepository.DeleteAsync(id, cancellationToken);
    }

}
