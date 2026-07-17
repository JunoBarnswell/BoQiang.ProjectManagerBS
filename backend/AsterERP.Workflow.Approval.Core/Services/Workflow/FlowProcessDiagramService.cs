using System.Text;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using Microsoft.Extensions.Caching.Memory;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class FlowProcessDiagramService : IFlowProcessDiagramService
{
    private readonly IMemoryCache _cache;
    private readonly IHistoryService _historyService;
    private readonly IRuntimeService _runtimeService;
    private readonly IRepositoryService _repositoryService;
    private readonly IExtendHisprocinstService _extendHisprocinstService;
    private readonly IBpmnModelService _bpmnModelService;

    public FlowProcessDiagramService(
        IMemoryCache cache,
        IHistoryService historyService,
        IRuntimeService runtimeService,
        IRepositoryService repositoryService,
        IExtendHisprocinstService extendHisprocinstService,
        IBpmnModelService bpmnModelService)
    {
        _cache = cache;
        _historyService = historyService;
        _runtimeService = runtimeService;
        _repositoryService = repositoryService;
        _extendHisprocinstService = extendHisprocinstService;
        _bpmnModelService = bpmnModelService;
    }

    public async Task<HighLightedNodeVo> CreateCacheHighLightedNodeVoByProcessInstanceIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        var vo = await FindHighLightedNodeVoByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        _cache.Set(processInstanceId, vo);
        return vo;
    }

    public async Task<HighLightedNodeVo> GetHighLightedNodeVoByProcessInstanceIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(processInstanceId, out HighLightedNodeVo cached))
        {
            return cached;
        }
        var vo = await FindHighLightedNodeVoByProcessInstanceIdAsync(processInstanceId, cancellationToken);
        _cache.Set(processInstanceId, vo);
        return vo;
    }

    public ActivityVo GetOneActivityVoByProcessInstanceIdAndActivityId(string processInstanceId, string activityId)
    {
        var key = $"{processInstanceId}-{activityId}";
        if (_cache.TryGetValue(key, out ActivityVo cached))
        {
            return cached;
        }
        var vo = new ActivityVo { Id = activityId, ProceInsId = processInstanceId };
        _cache.Set(key, vo);
        return vo;
    }

    public List<ActivityVo> GetProcessActivityVosByProcessInstanceId(string processInstanceId)
    {
        if (_cache.TryGetValue(processInstanceId, out List<ActivityVo> cached))
        {
            return cached;
        }
        var datas = new List<ActivityVo>();
        _cache.Set(processInstanceId, datas);
        return datas;
    }

    private async Task<HighLightedNodeVo> FindHighLightedNodeVoByProcessInstanceIdAsync(
        string processInstanceId,
        CancellationToken cancellationToken)
    {
        var activeActivityIds = new List<string>();
        var highLightedFlows = new List<string>();

        var historicSequenceFlows = _historyService.CreateHistoricActivityInstanceQuery()
            .ProcessInstanceId(processInstanceId)
            .ActivityType("sequenceFlow")
            .ListAsync(cancellationToken);
        var historicSequenceFlowItems = await historicSequenceFlows;
        foreach (var historicActivityInstance in historicSequenceFlowItems)
        {
            highLightedFlows.Add(historicActivityInstance.ActivityId);
        }

        string? processDefinitionId = null;
        string? modelName = null;

        var processInstances = await _runtimeService.GetExecutionsAsync(cancellationToken);
        var processInstance = processInstances
            .FirstOrDefault(e => e.ProcessInstanceId == processInstanceId || e.Id == processInstanceId);

        if (processInstance == null)
        {
            var extendHisprocinst = await _extendHisprocinstService
                .FindExtendHisprocinstByProcessInstanceIdAsync(processInstanceId, cancellationToken);
            processDefinitionId = extendHisprocinst?.ProcessDefinitionId;
            var historicEnds = _historyService.CreateHistoricActivityInstanceQuery()
                .ProcessInstanceId(processInstanceId)
                .ActivityType("endEvent")
                .ListAsync(cancellationToken);
            foreach (var historicActivityInstance in await historicEnds)
            {
                activeActivityIds.Add(historicActivityInstance.ActivityId);
            }
            modelName = extendHisprocinst?.ProcessName;
        }
        else
        {
            processDefinitionId = processInstance.ProcessDefinitionId;
            activeActivityIds = await _runtimeService.GetActiveActivityIdsAsync(processInstanceId, cancellationToken);
        }

        string? modelXml = null;
        if (!string.IsNullOrWhiteSpace(processDefinitionId))
        {
            var bpmnXmlBytes = await _repositoryService.GetProcessModelAsync(processDefinitionId, cancellationToken);
            if (bpmnXmlBytes != null)
            {
                modelXml = Encoding.UTF8.GetString(bpmnXmlBytes);
            }
        }

        return new HighLightedNodeVo(highLightedFlows, activeActivityIds, modelXml ?? string.Empty, modelName ?? string.Empty);
    }
}
