using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Model;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IFlowProcessDiagramService
{
    Task<HighLightedNodeVo> CreateCacheHighLightedNodeVoByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<HighLightedNodeVo> GetHighLightedNodeVoByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    ActivityVo GetOneActivityVoByProcessInstanceIdAndActivityId(string processInstanceId, string activityId);
    List<ActivityVo> GetProcessActivityVosByProcessInstanceId(string processInstanceId);
}
