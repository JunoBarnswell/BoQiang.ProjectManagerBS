using AsterERP.Workflow.Api.Shared;
using AsterERP.Workflow.Api.Task.Payload;
using AsterERP.Workflow.Core.Services;
using ApiTaskStatus = AsterERP.Workflow.Api.Task.Payload.TaskStatus;
using CoreTask = AsterERP.Workflow.Core.Services.TaskImplementation;

namespace AsterERP.Workflow.Api.Task.Runtime;

public class TaskRuntimeImplementation : ITaskRuntime
{
    private readonly ITaskService _taskService;

    public TaskRuntimeImplementation(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<TaskPayload> CreateTaskAsync(
        CreateTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        var coreTask = new CoreTask
        {
            Name = payload.Name,
            Description = payload.Description,
            Assignee = payload.Assignee,
            Priority = payload.Priority ?? 0,
            ProcessInstanceId = payload.ProcessInstanceId,
            FormKey = payload.FormKey,
            DueDate = payload.DueDate,
            Category = payload.Category
        };

        var created = await _taskService.CreateTaskAsync(coreTask, cancellationToken);
        return MapTask(created);
    }

    public async Task<TaskPayload> ClaimTaskAsync(
        ClaimTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        var assignee = payload.Assignee ?? throw new WorkflowApiException(400, "Assignee is required");
        await _taskService.ClaimTaskAsync(payload.TaskId, assignee, cancellationToken);
        var task = await _taskService.GetTaskAsync(payload.TaskId, cancellationToken);
        return MapTask(task!);
    }

    public async Task<TaskPayload> ReleaseTaskAsync(
        ReleaseTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        await _taskService.SetAssigneeAsync(payload.TaskId, null!, cancellationToken);
        var task = await _taskService.GetTaskAsync(payload.TaskId, cancellationToken);
        return MapTask(task!);
    }

    public async Task<TaskPayload> CompleteTaskAsync(
        CompleteTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        var task = await _taskService.GetTaskAsync(payload.TaskId, cancellationToken);
        if (task == null)
            throw new WorkflowNotFoundException($"Task '{payload.TaskId}' not found");

        var result = MapTask(task);
        await _taskService.CompleteTaskAsync(payload.TaskId, payload.Variables, cancellationToken);
        return WithStatus(result, ApiTaskStatus.Completed);
    }

    public async Task<TaskPayload> UpdateTaskAsync(
        UpdateTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (payload.Name != null)
            await _taskService.SetAssigneeAsync(payload.TaskId, payload.Name, cancellationToken);

        if (payload.Priority.HasValue)
            await _taskService.SetPriorityAsync(payload.TaskId, payload.Priority.Value, cancellationToken);

        if (payload.DueDate.HasValue)
            await _taskService.SetDueDateAsync(payload.TaskId, payload.DueDate, cancellationToken);

        if (payload.Owner != null)
            await _taskService.SetOwnerAsync(payload.TaskId, payload.Owner, cancellationToken);

        if (payload.Assignee != null)
            await _taskService.SetAssigneeAsync(payload.TaskId, payload.Assignee, cancellationToken);

        var task = await _taskService.GetTaskAsync(payload.TaskId, cancellationToken);
        return MapTask(task!);
    }

    public async Task<TaskPayload> DeleteTaskAsync(
        DeleteTaskPayload payload,
        CancellationToken cancellationToken = default)
    {
        var task = await _taskService.GetTaskAsync(payload.TaskId, cancellationToken);
        if (task == null)
            throw new WorkflowNotFoundException($"Task '{payload.TaskId}' not found");

        var result = MapTask(task);
        await _taskService.CompleteTaskAsync(payload.TaskId, cancellationToken: cancellationToken);
        return WithStatus(result, ApiTaskStatus.Deleted);
    }

    public async Task<IReadOnlyCollection<TaskPayload>> GetTasksAsync(
        CancellationToken cancellationToken = default)
    {
        var tasks = await _taskService.GetTasksAsync(cancellationToken);
        return tasks.Select(MapTask).ToList().AsReadOnly();
    }

    private static TaskPayload MapTask(CoreTask task) => new()
    {
        Id = task.Id,
        Name = task.Name,
        Description = task.Description,
        Priority = task.Priority,
        Assignee = task.Assignee,
        Owner = task.Owner,
        ProcessInstanceId = task.ProcessInstanceId,
        TaskDefinitionKey = task.TaskDefinitionKey,
        CreatedDate = task.CreateTime,
        DueDate = task.DueDate,
        Category = task.Category,
        FormKey = task.FormKey,
        Status = task.Assignee != null ? ApiTaskStatus.Assigned : ApiTaskStatus.Created
    };

    private static TaskPayload WithStatus(TaskPayload source, ApiTaskStatus status) => new()
    {
        Id = source.Id,
        Name = source.Name,
        Description = source.Description,
        Priority = source.Priority,
        Assignee = source.Assignee,
        Owner = source.Owner,
        ProcessInstanceId = source.ProcessInstanceId,
        ProcessDefinitionId = source.ProcessDefinitionId,
        ExecutionId = source.ExecutionId,
        TaskDefinitionKey = source.TaskDefinitionKey,
        CreatedDate = source.CreatedDate,
        ClaimedDate = source.ClaimedDate,
        DueDate = source.DueDate,
        CompletedDate = source.CompletedDate,
        Category = source.Category,
        FormKey = source.FormKey,
        TenantId = source.TenantId,
        Status = status,
        BusinessKey = source.BusinessKey,
        ParentTaskId = source.ParentTaskId
    };
}
