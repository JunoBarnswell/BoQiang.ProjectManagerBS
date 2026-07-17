using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public interface IWorkflowTaskRuntimeService
{
    Task<PagerModel<TaskVo>> GetAppingTasksPagerModelAsync(TaskQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<PagerModel<TaskVo>> GetApplyedTasksPagerModelAsync(TaskQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default);
    Task<ReturnVo<string>> CompleteAsync(CompleteTaskVo completeTaskVo, CancellationToken cancellationToken = default);
    Task<long> GetAppingTaskContAsync(TaskQueryParamsVo @params, CancellationToken cancellationToken = default);
    Task<TaskVo?> GetPendingTaskForUserAsync(string taskId, string appSn, string userCode, CancellationToken cancellationToken = default);
}
