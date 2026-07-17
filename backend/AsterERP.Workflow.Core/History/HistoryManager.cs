using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.History;

public interface IHistoryManager
{
    bool IsHistoryEnabled();
    global::System.Threading.Tasks.Task RecordActivityStartAsync(ExecutionEntity execution, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordActivityEndAsync(ExecutionEntity execution, string? deleteReason, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordProcessInstanceStartAsync(ExecutionEntity execution, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordProcessInstanceEndAsync(ExecutionEntity execution, string? deleteReason, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordTaskCreatedAsync(ExecutionEntity execution, string taskId, string taskName, string? assignee, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordTaskCompletedAsync(ExecutionEntity execution, TaskImplementation task, string? deleteReason, CancellationToken cancellationToken = default);
    global::System.Threading.Tasks.Task RecordVariableAsync(ExecutionEntity execution, string variableName, object? value, string? taskId, CancellationToken cancellationToken = default);
    void RecordActivityStart(ExecutionEntity execution);
    void RecordActivityEnd(ExecutionEntity execution, string? deleteReason);
    void RecordProcessInstanceStart(ExecutionEntity execution);
    void RecordProcessInstanceEnd(ExecutionEntity execution, string? deleteReason);
    void RecordTaskCreated(ExecutionEntity execution, string taskId, string taskName, string? assignee);
    void RecordTaskCompleted(ExecutionEntity execution, TaskImplementation task, string? deleteReason);
    void RecordVariable(ExecutionEntity execution, string variableName, object? value, string? taskId);
}

public enum HistoryLevel
{
    None = 0,
    Activity = 1,
    Audit = 2,
    Full = 3
}

public class DefaultHistoryManager : IHistoryManager
{
    private readonly HistoryLevel _historyLevel;
    private readonly IHistoricEntityService? _historicEntityService;
    public HistoricEntityServiceImplementation? HistoricEntityService => _historicEntityService as HistoricEntityServiceImplementation;

    public DefaultHistoryManager() : this(HistoryLevel.Audit, null) { }

    public DefaultHistoryManager(string historyLevel = "audit", IHistoricEntityService? historicEntityService = null)
    {
        _historyLevel = ParseHistoryLevel(historyLevel);
        _historicEntityService = historicEntityService;
    }

    public DefaultHistoryManager(HistoryLevel historyLevel = HistoryLevel.Audit, IHistoricEntityService? historicEntityService = null)
    {
        _historyLevel = historyLevel;
        _historicEntityService = historicEntityService;
    }

    public bool IsHistoryEnabled()
    {
        return _historyLevel != HistoryLevel.None;
    }

    public global::System.Threading.Tasks.Task RecordActivityStartAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historyLevel < HistoryLevel.Activity || _historicEntityService == null) return global::System.Threading.Tasks.Task.CompletedTask;

        var activityId = execution.CurrentActivityId ?? execution.CurrentFlowElement?.Id;
        if (activityId == null) return global::System.Threading.Tasks.Task.CompletedTask;

        return _historicEntityService.RecordActivityStartAsync(
            null,
            activityId,
            execution.CurrentActivityName ?? execution.CurrentFlowElement?.Name,
            execution.CurrentFlowElement?.GetType().Name ?? "Unknown",
            execution.Id,
            execution.ProcessInstanceId ?? "",
            cancellationToken);
    }

    public global::System.Threading.Tasks.Task RecordActivityEndAsync(ExecutionEntity execution, string? deleteReason, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historyLevel < HistoryLevel.Activity || _historicEntityService == null) return global::System.Threading.Tasks.Task.CompletedTask;

        var activityId = execution.CurrentActivityId ?? execution.CurrentFlowElement?.Id;
        if (activityId == null) return global::System.Threading.Tasks.Task.CompletedTask;

        return _historicEntityService.RecordActivityEndAsync(activityId, execution.Id, cancellationToken);
    }

    public global::System.Threading.Tasks.Task RecordProcessInstanceStartAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historicEntityService == null) return global::System.Threading.Tasks.Task.CompletedTask;

        var startUserId = execution.Variables.TryGetValue("initiator", out var initiatorValue)
            ? initiatorValue as string
            : null;

        return _historicEntityService.RecordProcessInstanceStartAsync(
            execution.ProcessInstanceId ?? execution.Id,
            execution.ProcessDefinitionId ?? "",
            execution.BusinessKey,
            startUserId,
            cancellationToken);
    }

    public global::System.Threading.Tasks.Task RecordProcessInstanceEndAsync(ExecutionEntity execution, string? deleteReason, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historicEntityService == null) return global::System.Threading.Tasks.Task.CompletedTask;

        return _historicEntityService.RecordProcessInstanceEndAsync(
            execution.ProcessInstanceId ?? execution.Id,
            deleteReason,
            cancellationToken);
    }

    public global::System.Threading.Tasks.Task RecordTaskCreatedAsync(ExecutionEntity execution, string taskId, string taskName, string? assignee, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historicEntityService == null) return global::System.Threading.Tasks.Task.CompletedTask;

        var runtimeTask = execution.TaskEntities.FirstOrDefault(task => task.Id == taskId);

        return _historicEntityService.RecordTaskCreatedAsync(
            taskId,
            runtimeTask?.Name ?? taskName,
            runtimeTask?.Assignee ?? assignee,
            runtimeTask?.ProcessInstanceId ?? execution.ProcessInstanceId ?? "",
            runtimeTask?.TaskDefinitionKey ?? execution.CurrentActivityId,
            cancellationToken);
    }

    public global::System.Threading.Tasks.Task RecordTaskCompletedAsync(ExecutionEntity execution, TaskImplementation task, string? deleteReason, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historicEntityService == null) return global::System.Threading.Tasks.Task.CompletedTask;

        return _historicEntityService.RecordTaskCompletedAsync(task.Id, task.Assignee, deleteReason, cancellationToken);
    }

    public global::System.Threading.Tasks.Task RecordVariableAsync(ExecutionEntity execution, string variableName, object? value, string? taskId, CancellationToken cancellationToken = default)
    {
        if (!IsHistoryEnabled() || _historyLevel < HistoryLevel.Audit || _historicEntityService == null || string.IsNullOrWhiteSpace(variableName)) return global::System.Threading.Tasks.Task.CompletedTask;

        return _historicEntityService.RecordVariableAsync(
            null,
            variableName,
            value,
            execution.ProcessInstanceId ?? execution.Id,
            taskId,
            includeDetail: _historyLevel >= HistoryLevel.Full,
            cancellationToken: cancellationToken);
    }

    public void RecordActivityStart(ExecutionEntity execution) => throw new NotSupportedException("RecordActivityStart is async-only. Use RecordActivityStartAsync.");
    public void RecordActivityEnd(ExecutionEntity execution, string? deleteReason) => throw new NotSupportedException("RecordActivityEnd is async-only. Use RecordActivityEndAsync.");
    public void RecordProcessInstanceStart(ExecutionEntity execution) => throw new NotSupportedException("RecordProcessInstanceStart is async-only. Use RecordProcessInstanceStartAsync.");
    public void RecordProcessInstanceEnd(ExecutionEntity execution, string? deleteReason) => throw new NotSupportedException("RecordProcessInstanceEnd is async-only. Use RecordProcessInstanceEndAsync.");
    public void RecordTaskCreated(ExecutionEntity execution, string taskId, string taskName, string? assignee) => throw new NotSupportedException("RecordTaskCreated is async-only. Use RecordTaskCreatedAsync.");
    public void RecordTaskCompleted(ExecutionEntity execution, TaskImplementation task, string? deleteReason) => throw new NotSupportedException("RecordTaskCompleted is async-only. Use RecordTaskCompletedAsync.");
    public void RecordVariable(ExecutionEntity execution, string variableName, object? value, string? taskId) => throw new NotSupportedException("RecordVariable is async-only. Use RecordVariableAsync.");

    private static HistoryLevel ParseHistoryLevel(string level)
    {
        return level?.ToLowerInvariant() switch
        {
            "none" => HistoryLevel.None,
            "activity" => HistoryLevel.Activity,
            "audit" => HistoryLevel.Audit,
            "full" => HistoryLevel.Full,
            _ => HistoryLevel.Audit
        };
    }
}
