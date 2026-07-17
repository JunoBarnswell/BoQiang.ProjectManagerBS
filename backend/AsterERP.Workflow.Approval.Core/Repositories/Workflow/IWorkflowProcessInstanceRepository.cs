using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.ProcessInstance;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Workflow;

public interface IWorkflowProcessInstanceRepository
{
    ISqlSugarClient Db { get; }
    Task<RefAsync<Page<ProcessInstanceVo>>> FindMyProcessinstancesPagerModelAsync(InstanceQueryParamsVo @params, int pageNum, int pageSize, CancellationToken cancellationToken = default);
}
