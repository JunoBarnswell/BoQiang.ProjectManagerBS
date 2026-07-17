using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Workflow;

public interface IWorkflowTaskRepository
{
    ISqlSugarClient Db { get; }
    Task<RefAsync<Page<TaskVo>>> GetApplyedTasksPagerModelAsync(TaskQueryParamsVo @params, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<RefAsync<Page<TaskVo>>> GetAppingTasksPagerModelAsync(TaskQueryParamsVo @params, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<long> GetAppingTaskContAsync(TaskQueryParamsVo @params, CancellationToken cancellationToken = default);
    Task<TaskVo?> GetPendingTaskForUserAsync(string taskId, string appSn, string userCode, CancellationToken cancellationToken = default);
    Task UpdateHisAssigneeAsync(string taskId, string assignee, CancellationToken cancellationToken = default);
}
