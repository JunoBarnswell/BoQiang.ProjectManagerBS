using AsterERP.Workflow.Api.Task.Payload;
using AsterERP.Workflow.Core.Services;
using ApiTaskStatus = AsterERP.Workflow.Api.Task.Payload.TaskStatus;
using CoreTask = AsterERP.Workflow.Core.Services.TaskImplementation;

namespace AsterERP.Workflow.Api.Task.Runtime;

public class TaskAdminRuntimeImplementation : ITaskAdminRuntime
{
    private readonly ITaskService _taskService;

    public TaskAdminRuntimeImplementation(ITaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<TaskPayload> ClaimTaskAsync(
        string taskId,
        string assignee,
        CancellationToken cancellationToken = default)
    {
        await _taskService.ClaimTaskAsync(taskId, assignee, cancellationToken);
        var task = await _taskService.GetTaskAsync(taskId, cancellationToken);
        return MapTask(task!);
    }

    public global::System.Threading.Tasks.Task DeleteTaskAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        return _taskService.CompleteTaskAsync(taskId, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyCollection<TaskPayload>> GetAllTasksAsync(
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
}
