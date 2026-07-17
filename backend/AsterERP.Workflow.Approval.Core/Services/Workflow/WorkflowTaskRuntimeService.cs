using AsterERP.Workflow.Core.Services;
using AsterERP.Workflow.Approval.Api.Enums.Workflow.Runtime;
using AsterERP.Workflow.Approval.Api.ViewModels.Workflow.Task;
using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Tools.Common;
using AsterERP.Workflow.Tools.Pager;
using AsterERP.Workflow.Tools.Vos;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Volo.Abp.Guids;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class WorkflowTaskRuntimeService : BaseProcessService, IWorkflowTaskRuntimeService
{
    private readonly IWorkflowTaskRepository _workflowTaskRepository;
    private readonly IWorkflowActivityInstanceService _workflowActivityInstanceService;
    private readonly ITaskService _taskService;
    private readonly ILogger<WorkflowTaskRuntimeService> _logger;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guidGenerator;

    public WorkflowTaskRuntimeService(
        IWorkflowTaskRepository workflowTaskRepository,
        IWorkflowActivityInstanceService workflowActivityInstanceService,
        ITaskService taskService,
        ICommentInfoService commentInfoService,
        IExtendHisprocinstService extendHisprocinstService,
        IMemoryCache cache,
        IClock clock,
        ILogger<WorkflowTaskRuntimeService> logger,
        IGuidGenerator guidGenerator)
        : base(commentInfoService, extendHisprocinstService, cache, clock)
    {
        _workflowTaskRepository = workflowTaskRepository;
        _workflowActivityInstanceService = workflowActivityInstanceService;
        _taskService = taskService;
        _logger = logger;
        _clock = clock;
        _guidGenerator = guidGenerator;
    }

    public async Task<PagerModel<TaskVo>> GetAppingTasksPagerModelAsync(TaskQueryParamsVo @paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await _workflowTaskRepository.GetAppingTasksPagerModelAsync(paramsVo, pageNum, pageSize, cancellationToken);
        return new PagerModel<TaskVo>(result.Value.TotalElements, result.Value.Content?.ToList() ?? new List<TaskVo>());
    }

    public async Task<PagerModel<TaskVo>> GetApplyedTasksPagerModelAsync(TaskQueryParamsVo paramsVo, int pageNum, int pageSize, CancellationToken cancellationToken = default)
    {
        var result = await _workflowTaskRepository.GetApplyedTasksPagerModelAsync(paramsVo, pageNum, pageSize, cancellationToken);
        return new PagerModel<TaskVo>(result.Value.TotalElements, result.Value.Content?.ToList() ?? new List<TaskVo>());
    }

    public async Task<ReturnVo<string>> CompleteAsync(CompleteTaskVo completeTaskVo, CancellationToken cancellationToken = default)
    {
        var returnVo = new ReturnVo<string>(ReturnCode.SUCCESS, "OK");
        try
        {
            if (completeTaskVo == null || string.IsNullOrWhiteSpace(completeTaskVo.TaskId))
            {
                return new ReturnVo<string>(ReturnCode.FAIL, "taskId should not be null");
            }

            var task = await _taskService.GetTaskAsync(completeTaskVo.TaskId, cancellationToken);
            if (task == null)
            {
                return new ReturnVo<string>(ReturnCode.FAIL, "没有查询到任务!");
            }

            if (string.IsNullOrWhiteSpace(completeTaskVo.ProcessInstanceId))
            {
                completeTaskVo.ProcessInstanceId = task.ProcessInstanceId;
            }

            EvictHighLightedNodeCache(task.ProcessInstanceId!);
            EvictOneActivityVoCache(task.ProcessInstanceId!, task.TaskDefinitionKey!);

            var taskId = completeTaskVo.TaskId;
            if ("PENDING".Equals(task.DelegationState))
            {
                var subTask = await CreateSubTaskAsync(task, task.ParentTaskId, completeTaskVo.UserCode, cancellationToken);
                await _taskService.CompleteTaskAsync(subTask.Id, null, cancellationToken);
                taskId = subTask.Id;
                var resolveVariables = WorkflowVariableValueConverter.Normalize(completeTaskVo.Variables);
                await _taskService.ResolveTaskAsync(completeTaskVo.TaskId, resolveVariables, cancellationToken);
            }
            else
            {
                await _workflowTaskRepository.UpdateHisAssigneeAsync(taskId, completeTaskVo.UserCode, cancellationToken);
                await _workflowActivityInstanceService.FinishRuntimeTaskAsync(taskId, cancellationToken);
                var completeVariables = WorkflowVariableValueConverter.Normalize(completeTaskVo.Variables);
                await _taskService.CompleteTaskAsync(completeTaskVo.TaskId, completeVariables, cancellationToken);
                await _workflowActivityInstanceService.SyncRuntimeTasksAsync(task.ProcessInstanceId!, cancellationToken);

                if (!string.IsNullOrWhiteSpace(task.ParentTaskId))
                {
                    var subTasks = await _taskService.GetSubTasksAsync(task.ParentTaskId, cancellationToken);
                    if (subTasks == null || subTasks.Count == 0)
                    {
                        var parentTask = await _taskService.GetTaskAsync(task.ParentTaskId, cancellationToken);
                        if (parentTask != null)
                        {
                            await _taskService.ResolveTaskAsync(task.ParentTaskId, null, cancellationToken);
                        }
                    }
                }
            }

            completeTaskVo.TaskId = taskId;
            completeTaskVo.ActivityId = task.TaskDefinitionKey;
            completeTaskVo.ActivityName = task.Name;
            await AddFlowCommentInfoAndProcessStatusAsync(completeTaskVo, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "WorkflowTaskRuntimeService - complete");
            throw new ValidationException("审批任务报错", ErrorCodes.WorkflowActionInvalid);
        }
        return returnVo;
    }

    public async Task<long> GetAppingTaskContAsync(TaskQueryParamsVo @params, CancellationToken cancellationToken = default)
    {
        return await _workflowTaskRepository.GetAppingTaskContAsync(@params, cancellationToken);
    }

    public async Task<TaskVo?> GetPendingTaskForUserAsync(string taskId, string appSn, string userCode, CancellationToken cancellationToken = default)
    {
        return await _workflowTaskRepository.GetPendingTaskForUserAsync(taskId, appSn, userCode, cancellationToken);
    }

    private async Task<TaskImplementation> CreateSubTaskAsync(TaskImplementation ptask, string? parentTaskId, string assignee, CancellationToken cancellationToken)
    {
        var task = new TaskImplementation
        {
            Id = _guidGenerator.Create().ToString("N"),
            Assignee = assignee,
            ProcessInstanceId = ptask.ProcessInstanceId,
            ProcessDefinitionId = ptask.ProcessDefinitionId,
            ParentTaskId = parentTaskId,
            Category = ptask.Category,
            Description = ptask.Description,
            Name = ptask.Name,
            TaskDefinitionKey = ptask.TaskDefinitionKey,
            CreateTime = _clock.Now,
            Priority = ptask.Priority
        };
        return await _taskService.CreateTaskAsync(task, cancellationToken);
    }
}
