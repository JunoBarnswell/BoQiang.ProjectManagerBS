using AsterERP.Workflow.Approval.Core.Repositories.Workflow;
using AsterERP.Workflow.Persistence.Entities;
using AsterERP.Workflow.Approval.Api.Models.Workflow;
using SqlSugar;
using Volo.Abp.Timing;

namespace AsterERP.Workflow.Approval.Core.Services.Workflow;

public class WorkflowActivityInstanceService : IWorkflowActivityInstanceService
{
    private static readonly DateTime ActiveEndTime = new(1900, 1, 1);

    private readonly IWorkflowRuntimeActivityRepository _actRuActinstRepository;
    private readonly IWorkflowHistoricActivityRepository _actHiActinstRepository;
    private readonly ISqlSugarClient _db;
    private readonly IClock _clock;

    public WorkflowActivityInstanceService(
        IWorkflowRuntimeActivityRepository actRuActinstRepository,
        IWorkflowHistoricActivityRepository actHiActinstRepository,
        ISqlSugarClient db,
        IClock clock)
    {
        _actRuActinstRepository = actRuActinstRepository;
        _actHiActinstRepository = actHiActinstRepository;
        _db = db;
        _clock = clock;
    }

    public async Task SyncRuntimeTasksAsync(string processInstanceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processInstanceId))
        {
            return;
        }

        var tasks = await _db.Queryable<TaskEntity>()
            .Where(task => task.ProcessInstanceId == processInstanceId)
            .ToListAsync(cancellationToken);

        foreach (var task in tasks)
        {
            await EnsureRuntimeActivityAsync(task, cancellationToken);
            await EnsureHistoricActivityAsync(task, cancellationToken);
        }
    }

    public async Task FinishRuntimeTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            return;
        }

        var finishedAt = _clock.Now;
        var activeEndTime = ActiveEndTime;
        var activeEndTimeUpperBound = activeEndTime.AddDays(1);
        await _db.Updateable<WorkflowRuntimeActivityRecord>()
            .SetColumns(activity => activity.EndTime == finishedAt)
            .Where(activity => activity.TaskId == taskId && activity.EndTime <= activeEndTimeUpperBound)
            .ExecuteCommandAsync(cancellationToken);

        await _db.Updateable<WorkflowHistoricActivityRecord>()
            .SetColumns(activity => activity.EndTime == finishedAt)
            .Where(activity => activity.TaskId == taskId && activity.EndTime <= activeEndTimeUpperBound)
            .ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteActinstByIdsAsync(List<string> actIds, CancellationToken cancellationToken = default)
    {
        foreach (var id in actIds)
        {
            await _actRuActinstRepository.DeleteAsync(id, cancellationToken);
            await _actHiActinstRepository.DeleteAsync(id, cancellationToken);
        }
    }

    private async Task EnsureRuntimeActivityAsync(TaskEntity task, CancellationToken cancellationToken)
    {
        var exists = await _db.Queryable<WorkflowRuntimeActivityRecord>()
            .AnyAsync(activity => activity.TaskId == task.Id, cancellationToken);
        if (exists)
        {
            return;
        }

        await _actRuActinstRepository.InsertAsync(CreateRuntimeActivity(task), cancellationToken);
    }

    private async Task EnsureHistoricActivityAsync(TaskEntity task, CancellationToken cancellationToken)
    {
        var exists = await _db.Queryable<WorkflowHistoricActivityRecord>()
            .AnyAsync(activity => activity.TaskId == task.Id, cancellationToken);
        if (exists)
        {
            return;
        }

        await _actHiActinstRepository.InsertAsync(CreateHistoricActivity(task), cancellationToken);
    }

    private WorkflowRuntimeActivityRecord CreateRuntimeActivity(TaskEntity task)
    {
        return new WorkflowRuntimeActivityRecord
        {
            Id = $"act-{task.Id}",
            Rev = 1,
            ProcDefId = task.ProcessDefinitionId ?? string.Empty,
            ProcInstId = task.ProcessInstanceId ?? string.Empty,
            ExecutionId = task.ExecutionId ?? string.Empty,
            ActId = task.TaskDefinitionKey ?? string.Empty,
            TaskId = task.Id,
            CallProcInstId = string.Empty,
            ActName = task.Name ?? string.Empty,
            ActType = "userTask",
            Assignee = task.Assignee ?? string.Empty,
            StartTime = task.CreateTime ?? _clock.Now,
            EndTime = ActiveEndTime,
            Duration = string.Empty,
            TransactionOrder = 0,
            DeleteReason = string.Empty,
            TenantId = task.TenantId ?? string.Empty
        };
    }

    private WorkflowHistoricActivityRecord CreateHistoricActivity(TaskEntity task)
    {
        return new WorkflowHistoricActivityRecord
        {
            Id = $"act-{task.Id}",
            Rev = 1,
            ProcDefId = task.ProcessDefinitionId ?? string.Empty,
            ProcInstId = task.ProcessInstanceId ?? string.Empty,
            ExecutionId = task.ExecutionId ?? string.Empty,
            ActId = task.TaskDefinitionKey ?? string.Empty,
            TaskId = task.Id,
            CallProcInstId = string.Empty,
            ActName = task.Name ?? string.Empty,
            ActType = "userTask",
            Assignee = task.Assignee ?? string.Empty,
            StartTime = task.CreateTime ?? _clock.Now,
            EndTime = ActiveEndTime,
            Duration = string.Empty,
            TransactionOrder = 0,
            DeleteReason = string.Empty,
            TenantId = task.TenantId ?? string.Empty
        };
    }
}
