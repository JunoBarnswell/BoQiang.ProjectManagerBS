using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Helper;

public class ListenerNotificationHelper
{
    private readonly IExpressionManager? _expressionManager;
    private readonly IServiceProvider? _serviceProvider;

    public ListenerNotificationHelper() { }

    public ListenerNotificationHelper(IExpressionManager? expressionManager, IServiceProvider? serviceProvider = null)
    {
        _expressionManager = expressionManager;
        _serviceProvider = serviceProvider;
    }

    public async Task ExecuteExecutionListeners(
        BpmnModelNs.FlowElement elementWithExecutionListeners,
        ExecutionEntity executionEntity,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (elementWithExecutionListeners.ExecutionListeners == null ||
            elementWithExecutionListeners.ExecutionListeners.Count == 0)
            return;

        executionEntity.EventName = eventType;
        try
        {
            foreach (var listener in elementWithExecutionListeners.ExecutionListeners)
            {
                if (listener.Event == eventType)
                {
                    await ExecuteListenerAsync(listener, executionEntity, null, cancellationToken);
                }
            }
        }
        finally
        {
            executionEntity.EventName = null;
        }
    }

    public Task ExecuteTaskListeners(
        BpmnModelNs.UserTask userTask,
        ExecutionEntity executionEntity,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        return ExecuteTaskListeners(userTask, executionEntity, null, eventType, cancellationToken);
    }

    public async Task ExecuteTaskListeners(
        BpmnModelNs.UserTask userTask,
        ExecutionEntity executionEntity,
        TaskImplementation? taskEntity,
        string eventType,
        CancellationToken cancellationToken = default)
    {
        if (userTask.TaskListeners == null || userTask.TaskListeners.Count == 0)
            return;

        executionEntity.EventName = eventType;
        try
        {
            foreach (var listener in userTask.TaskListeners)
            {
                if (listener.Event == eventType)
                {
                    await ExecuteListenerAsync(listener, executionEntity, taskEntity, cancellationToken);
                }
            }
        }
        finally
        {
            executionEntity.EventName = null;
        }
    }

    private Task ExecuteListenerAsync(
        BpmnModelNs.WorkflowExtensionListener listener,
        ExecutionEntity execution,
        TaskImplementation? taskEntity,
        CancellationToken cancellationToken)
    {
        if (listener.ImplementationType == "class" && !string.IsNullOrEmpty(listener.Implementation))
        {
            return ExecuteClassListenerAsync(listener.Implementation, execution, taskEntity, cancellationToken);
        }

        else if (listener.ImplementationType == "delegateExpression" && !string.IsNullOrEmpty(listener.Implementation))
        {
            return ExecuteDelegateExpressionListenerAsync(listener.Implementation, execution, taskEntity, cancellationToken);
        }
        else if (listener.ImplementationType == "expression" && !string.IsNullOrEmpty(listener.Implementation))
        {
            _expressionManager?.Evaluate(listener.Implementation, execution.Variables);
        }

        return Task.CompletedTask;
    }

    private async Task ExecuteClassListenerAsync(string implementation, ExecutionEntity execution, TaskImplementation? taskEntity, CancellationToken cancellationToken)
    {
        var instance = ClassDelegateUtil.Instantiate(implementation, _serviceProvider);
        await ExecuteListenerInstanceAsync(instance, execution, taskEntity, cancellationToken);
    }

    private async Task ExecuteDelegateExpressionListenerAsync(string implementation, ExecutionEntity execution, TaskImplementation? taskEntity, CancellationToken cancellationToken)
    {
        var instance = DelegateExpressionUtil.ResolveDelegateExpression(implementation, _expressionManager, execution.Variables, _serviceProvider);
        await ExecuteListenerInstanceAsync(instance, execution, taskEntity, cancellationToken);
    }

    private static async Task ExecuteListenerInstanceAsync(object instance, ExecutionEntity execution, TaskImplementation? taskEntity, CancellationToken cancellationToken)
    {
        if (taskEntity != null && instance is Delegate.ITaskListener taskListener)
        {
            await taskListener.NotifyAsync(new TaskListenerContext(taskEntity, execution), cancellationToken);
            return;
        }

        if (instance is Delegate.IExecutionListener executionListener)
        {
            await executionListener.NotifyAsync(new Delegate.DelegateExecution(execution), cancellationToken);
            return;
        }

        if (instance is Delegate.IWorkflowDelegate workflowDelegate)
        {
            await workflowDelegate.ExecuteAsync(new Delegate.DelegateExecution(execution));
            return;
        }

        if (instance is Event.IWorkflowEventListener eventListener)
        {
            eventListener.OnEvent(new Event.WorkflowEventImplementation(
                Event.WorkflowEventType.CUSTOM,
                execution.Id,
                execution.ProcessInstanceId,
                execution.ProcessDefinitionId));
        }
    }

    private sealed class TaskListenerContext : IDelegateTask
    {
        private readonly TaskImplementation _task;
        private readonly ExecutionEntity _execution;

        public TaskListenerContext(TaskImplementation task, ExecutionEntity execution)
        {
            _task = task;
            _execution = execution;
            Name = task.Name;
            Description = task.Description;
            Priority = task.Priority;
            FormKey = task.FormKey;
            Owner = task.Owner;
            Assignee = task.Assignee;
            DueDate = task.DueDate;
            Category = task.Category;
        }

        public Dictionary<string, object?> Variables => _execution.Variables;
        public string Id => _task.Id;
        public string? Name { get; set; }
        public string? Description { get; set; }
        public int Priority { get; set; }
        public string? ProcessInstanceId => _task.ProcessInstanceId;
        public string? ExecutionId => _execution.Id;
        public string? ProcessDefinitionId => _task.ProcessDefinitionId;
        public DateTime? CreateTime => _task.CreateTime;
        public string? TaskDefinitionKey => _task.TaskDefinitionKey;
        public bool IsSuspended { get; set; }
        public string? TenantId => _execution.TenantId;
        public string? FormKey { get; set; }
        public IDelegateExecution? Execution => new DelegateExecution(_execution);
        public string? EventName => _execution.EventName;
        public string? Owner { get; set; }
        public string? Assignee { get; set; }
        public DateTime? DueDate { get; set; }
        public string? Category { get; set; }

        public object? GetVariable(string name) => _execution.GetVariable(name);
        public void SetVariable(string name, object? value) => _execution.SetVariable(name, value);
        public object? GetVariableLocal(string variableName) => _execution.GetVariableLocal(variableName);
        public void SetVariableLocal(string variableName, object? value) => _execution.SetVariableLocal(variableName, value);
        public bool HasVariable(string variableName) => _execution.HasVariable(variableName);
        public bool HasVariableLocal(string variableName) => _execution.HasVariableLocal(variableName);
        public void RemoveVariable(string variableName) => _execution.RemoveVariable(variableName);
        public void RemoveVariableLocal(string variableName) => _execution.RemoveVariableLocal(variableName);
        public void SetVariables(Dictionary<string, object?> variables) => _execution.SetVariables(variables);
        public void SetVariablesLocal(Dictionary<string, object?> variables) => _execution.SetVariablesLocal(variables);
        public void AddCandidateUser(string userId) { }
        public void AddCandidateUsers(IEnumerable<string> candidateUsers) { }
        public void AddCandidateGroup(string groupId) { }
        public void AddCandidateGroups(IEnumerable<string> candidateGroups) { }
        public void DeleteCandidateUser(string userId) { }
        public void DeleteCandidateGroup(string groupId) { }
        public void AddUserIdentityLink(string userId, string identityLinkType) { }
        public void AddGroupIdentityLink(string groupId, string identityLinkType) { }
        public void DeleteUserIdentityLink(string userId, string identityLinkType) { }
        public void DeleteGroupIdentityLink(string groupId, string identityLinkType) { }
    }
}
