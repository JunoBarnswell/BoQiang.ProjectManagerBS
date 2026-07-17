using AsterERP.Workflow.Api.Process.Payload;
using AsterERP.Workflow.Approval.Api.Models.Org;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Runtime;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IWorkflowProcessInstanceRuntimeService
{
    Task<ReturnVo<ProcessInstancePayload>> StartProcessInstanceByKeyAsync(StartProcessInstanceVo @params, CancellationToken cancellationToken = default);
    Task<Dictionary<string, object>> GetStartVariablesAsync(StartProcessInstanceVo @params, Personal personal, CancellationToken cancellationToken = default);
    Task<StartorBaseInfoVo?> GetStartorBaseInfoVoByProcessInstanceIdAsync(string processInstanceId, CancellationToken cancellationToken = default);
    Task<PagerModel<ProcessInstanceVo>> FindMyProcessinstancesPagerModelAsync(InstanceQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> StopProcessAsync(EndVo endVo, CancellationToken cancellationToken = default);
}
