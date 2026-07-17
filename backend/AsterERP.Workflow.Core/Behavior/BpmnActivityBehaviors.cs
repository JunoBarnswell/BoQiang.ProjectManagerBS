using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.DynamicBpmn;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Helper;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public interface IBpmnActivityBehavior
{
    global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default);
}

public class StartEventActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly ListenerNotificationHelper? _listenerNotificationHelper;
    private readonly IExpressionManager? _expressionManager;

    public StartEventActivityBehavior() { }

    public StartEventActivityBehavior(
        IEventDispatcher? eventDispatcher = null,
        ListenerNotificationHelper? listenerNotificationHelper = null,
        IExpressionManager? expressionManager = null)
    {
        _eventDispatcher = eventDispatcher;
        _listenerNotificationHelper = listenerNotificationHelper;
        _expressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (_listenerNotificationHelper != null && execution.CurrentFlowElement != null)
        {
            await _listenerNotificationHelper.ExecuteExecutionListeners(
                execution.CurrentFlowElement,
                execution,
                "start",
                cancellationToken);
        }

        if (_eventDispatcher != null && _eventDispatcher.IsEnabled && execution.CurrentFlowElement != null)
        {
            _eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateActivityStartedEvent(
                execution.CurrentFlowElement.Id,
                execution.CurrentFlowElement.GetType().Name,
                execution.Id,
                execution.ProcessInstanceId ?? string.Empty));
        }

        await LeaveAsync(execution, _expressionManager, cancellationToken);
    }
}

public class NoneStartEventActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IExpressionManager? _expressionManager;

    public NoneStartEventActivityBehavior(IExpressionManager? expressionManager = null)
    {
        _expressionManager = expressionManager;
    }

    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        await LeaveAsync(execution, _expressionManager, cancellationToken);
    }
}

public class EndEventActivityBehavior : FlowNodeActivityBehavior
{
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly ListenerNotificationHelper? _listenerNotificationHelper;

    public EndEventActivityBehavior() { }

    public EndEventActivityBehavior(IEventDispatcher? eventDispatcher = null, ListenerNotificationHelper? listenerNotificationHelper = null)
    {
        _eventDispatcher = eventDispatcher;
        _listenerNotificationHelper = listenerNotificationHelper;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (_listenerNotificationHelper != null && execution.CurrentFlowElement != null)
        {
            await _listenerNotificationHelper.ExecuteExecutionListeners(
                execution.CurrentFlowElement,
                execution,
                "end",
                cancellationToken);
        }

        if (_eventDispatcher != null && _eventDispatcher.IsEnabled)
        {
            _eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateActivityCompletedEvent(
                execution.CurrentFlowElementId ?? execution.Id,
                execution.CurrentFlowElement?.GetType().Name ?? "endEvent",
                execution.Id,
                execution.ProcessInstanceId ?? string.Empty));

            _eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateProcessCompletedEvent(
                execution.ProcessInstanceId ?? execution.Id,
                execution.ProcessDefinitionId ?? string.Empty));
        }

        execution.IsActive = false;
        execution.IsEnded = true;
        await Task.CompletedTask;
    }
}

public class TerminateEndEventActivityBehavior : IBpmnActivityBehavior
{
    private readonly IEventDispatcher? _eventDispatcher;

    public bool TerminateAll { get; set; }
    public bool TerminateMultiInstance { get; set; }

    public TerminateEndEventActivityBehavior() { }

    public TerminateEndEventActivityBehavior(IEventDispatcher? eventDispatcher = null, bool terminateAll = false, bool terminateMultiInstance = false)
    {
        _eventDispatcher = eventDispatcher;
        TerminateAll = terminateAll;
        TerminateMultiInstance = terminateMultiInstance;
    }

    public async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var deleteReason = CreateDeleteReason(execution.CurrentActivityId);

        if (TerminateAll)
        {
            await TerminateAllAsync(execution, deleteReason, cancellationToken);
        }
        else if (TerminateMultiInstance)
        {
            await TerminateMultiInstanceRootAsync(execution, deleteReason, cancellationToken);
        }
        else
        {
            await DefaultTerminateEndEventAsync(execution, deleteReason, cancellationToken);
        }
    }

    protected virtual async Task TerminateAllAsync(ExecutionEntity execution, string deleteReason, CancellationToken cancellationToken)
    {
        var rootExecution = FindRootExecution(execution);
        TerminateChildExecutions(rootExecution, deleteReason);

        if (_eventDispatcher != null && _eventDispatcher.IsEnabled)
        {
            _eventDispatcher.DispatchEvent(new WorkflowEntityEvent(
                WorkflowEventType.PROCESS_CANCELLED,
                rootExecution,
                rootExecution.Id,
                rootExecution.ProcessInstanceId,
                rootExecution.ProcessDefinitionId));

            _eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateProcessCompletedEvent(
                rootExecution.ProcessInstanceId ?? rootExecution.Id,
                rootExecution.ProcessDefinitionId ?? string.Empty));
        }

        rootExecution.IsActive = false;
        rootExecution.IsEnded = true;
        await Task.CompletedTask;
    }

    protected virtual async Task DefaultTerminateEndEventAsync(ExecutionEntity execution, string deleteReason, CancellationToken cancellationToken)
    {
        if (_eventDispatcher != null && _eventDispatcher.IsEnabled)
        {
            _eventDispatcher.DispatchEvent(new WorkflowEntityEvent(
                WorkflowEventType.PROCESS_CANCELLED,
                execution,
                execution.Id,
                execution.ProcessInstanceId,
                execution.ProcessDefinitionId));
        }

        var scopeExecution = FindScopeExecution(execution);
        if (scopeExecution != null)
        {
            TerminateChildExecutions(scopeExecution, deleteReason);
            scopeExecution.IsActive = false;
            scopeExecution.IsEnded = true;
        }

        execution.IsActive = false;
        execution.IsEnded = true;
        await Task.CompletedTask;
    }

    protected virtual async Task TerminateMultiInstanceRootAsync(ExecutionEntity execution, string deleteReason, CancellationToken cancellationToken)
    {
        var miRootExecution = FindMultiInstanceRoot(execution);
        if (miRootExecution != null)
        {
            TerminateChildExecutions(miRootExecution, deleteReason);
            miRootExecution.IsActive = false;
            miRootExecution.IsEnded = true;
        }
        else
        {
            await DefaultTerminateEndEventAsync(execution, deleteReason, cancellationToken);
            return;
        }

        execution.IsActive = false;
        execution.IsEnded = true;
        await Task.CompletedTask;
    }

    protected virtual void TerminateChildExecutions(ExecutionEntity execution, string deleteReason)
    {
        foreach (var childExecution in execution.ChildExecutions.ToList())
        {
            TerminateChildExecutions(childExecution, deleteReason);
            childExecution.IsActive = false;
            childExecution.IsEnded = true;
        }
    }

    protected virtual ExecutionEntity FindRootExecution(ExecutionEntity execution)
    {
        var current = execution;
        while (current.Parent != null)
        {
            current = current.Parent;
        }
        return current;
    }

    protected virtual ExecutionEntity? FindScopeExecution(ExecutionEntity execution)
    {
        var current = execution.Parent;
        while (current != null)
        {
            if (current.IsScope)
            {
                return current;
            }
            current = current.Parent;
        }
        return execution;
    }

    protected virtual ExecutionEntity? FindMultiInstanceRoot(ExecutionEntity execution)
    {
        var current = execution.Parent;
        while (current != null)
        {
            if (current.GetVariableLocal("nrOfInstances") != null)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    public static string CreateDeleteReason(string? activityId)
    {
        return activityId != null
            ? $"terminateEndEvent: {activityId}"
            : "terminateEndEvent";
    }
}

public class UserTaskActivityBehavior : FlowNodeActivityBehavior
{
    private readonly BpmnModelNs.UserTask _userTask;
    private readonly IExpressionManager? _expressionManager;
    private readonly IEventDispatcher? _eventDispatcher;
    private readonly ListenerNotificationHelper? _listenerNotificationHelper;

    public UserTaskActivityBehavior(
        BpmnModelNs.UserTask userTask,
        IExpressionManager? expressionManager = null,
        IEventDispatcher? eventDispatcher = null,
        ListenerNotificationHelper? listenerNotificationHelper = null)
    {
        _userTask = userTask;
        _expressionManager = expressionManager;
        _eventDispatcher = eventDispatcher;
        _listenerNotificationHelper = listenerNotificationHelper;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var taskEntity = new TaskImplementation
        {
            Id = $"task-{AbpTimeIdProvider.NewGuid("N")}",
            Name = ResolveExpression(_userTask.Name, execution),
            Description = ResolveExpression(_userTask.Documentation, execution),
            TaskDefinitionKey = _userTask.Id,
            ProcessInstanceId = execution.ProcessInstanceId,
            ProcessDefinitionId = execution.ProcessDefinitionId,
            Assignee = ResolveExpression(_userTask.Assignee, execution),
            Owner = ResolveExpression(_userTask.Owner, execution),
            Priority = _userTask.Priority ?? 50,
            Category = _userTask.Category,
            FormKey = ResolveExpression(_userTask.FormKey, execution),
            CreateTime = AbpTimeIdProvider.UtcNow,
            CandidateUsers = ResolveCandidateExpressions(_userTask.CandidateUsers, execution),
            CandidateGroups = ResolveCandidateExpressions(_userTask.CandidateGroups, execution)
        };

        execution.TaskEntities.Add(taskEntity);

        if (_listenerNotificationHelper != null)
        {
            await _listenerNotificationHelper.ExecuteTaskListeners(
                _userTask,
                execution,
                taskEntity,
                "create",
                cancellationToken);
        }

        if (_eventDispatcher != null && _eventDispatcher.IsEnabled)
        {
            _eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateTaskCreatedEvent(
                taskEntity.Id,
                taskEntity.Assignee,
                execution.ProcessInstanceId ?? string.Empty));

            if (!string.IsNullOrEmpty(taskEntity.Assignee))
            {
                _eventDispatcher.DispatchEvent(WorkflowEventBuilder.CreateTaskAssignedEvent(
                    taskEntity.Id,
                    taskEntity.Assignee,
                    execution.ProcessInstanceId ?? string.Empty));
            }
        }

        await Task.CompletedTask;
    }

    private string? ResolveExpression(string? expression, ExecutionEntity execution)
    {
        if (string.IsNullOrEmpty(expression)) return expression;
        if (_expressionManager == null) return expression;

        if ((expression.StartsWith("${") && expression.EndsWith("}")) ||
            (expression.StartsWith("#{") && expression.EndsWith("}")))
        {
            var result = _expressionManager.Evaluate(expression, execution.Variables);
            return result?.ToString();
        }

        return expression;
    }

    private List<string> ResolveCandidateExpressions(IEnumerable<string>? candidates, ExecutionEntity execution)
    {
        if (candidates is null)
        {
            return [];
        }

        var resolved = new List<string>();
        foreach (var candidate in candidates)
        {
            AppendResolvedCandidate(resolved, ResolveCandidateExpression(candidate, execution));
        }

        return resolved
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private object? ResolveCandidateExpression(string? expression, ExecutionEntity execution)
    {
        if (string.IsNullOrWhiteSpace(expression) || _expressionManager == null)
        {
            return expression;
        }

        if ((expression.StartsWith("${") && expression.EndsWith("}")) ||
            (expression.StartsWith("#{") && expression.EndsWith("}")))
        {
            return _expressionManager.Evaluate(expression, execution.Variables);
        }

        return expression;
    }

    private static void AppendResolvedCandidate(List<string> target, object? value)
    {
        switch (value)
        {
            case null:
                return;
            case string text:
                target.AddRange(text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(item => !string.IsNullOrWhiteSpace(item)));
                return;
            case IEnumerable<string> strings:
                target.AddRange(strings.Where(item => !string.IsNullOrWhiteSpace(item)));
                return;
            case IEnumerable values:
                foreach (var item in values)
                {
                    AppendResolvedCandidate(target, item);
                }
                return;
            default:
                var candidate = Convert.ToString(value);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    target.Add(candidate);
                }
                return;
        }
    }
}

public class ServiceTaskActivityBehavior : FlowNodeActivityBehavior
{
    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }
}

public class ScriptTaskActivityBehavior : FlowNodeActivityBehavior
{
    public string? Script { get; set; }
    public string? ScriptFormat { get; set; }
    public bool AutoStoreVariables { get; set; }
    public string? ResultVariable { get; set; }
    public bool ThrowException { get; set; }

    public IExpressionManager? ExpressionManager { get; set; }
    public IDynamicBpmnService? DynamicBpmnService { get; set; }

    public ScriptTaskActivityBehavior() { }

    public ScriptTaskActivityBehavior(
        string? script = null,
        string? scriptFormat = null,
        bool autoStoreVariables = false,
        string? resultVariable = null,
        IExpressionManager? expressionManager = null,
        IDynamicBpmnService? dynamicBpmnService = null)
    {
        Script = script;
        ScriptFormat = scriptFormat;
        AutoStoreVariables = autoStoreVariables;
        ResultVariable = resultVariable;
        ExpressionManager = expressionManager;
        DynamicBpmnService = dynamicBpmnService;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var scriptToExecute = ResolveScript(execution);
        if (string.IsNullOrEmpty(scriptToExecute))
        {
            throw new WorkflowEngineException("No script provided for script task");
        }

        try
        {
            var result = ExecuteScript(scriptToExecute, execution);

            if (result != null && !string.IsNullOrEmpty(ResultVariable))
            {
                execution.SetVariable(ResultVariable, result);
            }

            if (AutoStoreVariables)
            {
                StoreScriptVariables(execution);
            }

            execution.IsActive = false;
            await LeaveAsync(execution, cancellationToken);
        }
        catch (Exception ex)
        {
            if (ThrowException)
            {
                throw new WorkflowEngineException($"Script task execution failed: {ex.Message}", ex);
            }
            execution.SetVariable("_scriptError", ex.Message);
            execution.SetVariable("_scriptStackTrace", ex.StackTrace);
            execution.IsActive = false;
            await LeaveAsync(execution, cancellationToken);
        }
    }

    protected virtual string? ResolveScript(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(Script))
        {
            if (ExpressionManager != null && (Script.StartsWith("${") || Script.StartsWith("#{")))
            {
                var result = ExpressionManager.Evaluate(Script, execution.Variables);
                return result?.ToString();
            }
            return Script;
        }

        if (DynamicBpmnService != null && !string.IsNullOrEmpty(execution.CurrentActivityId))
        {
            var dynamicScript = DynamicBpmnService.GetScriptTaskScript(execution.CurrentActivityId, execution.Variables);
            if (!string.IsNullOrEmpty(dynamicScript))
            {
                return dynamicScript;
            }
        }

        return null;
    }

    protected virtual object? ExecuteScript(string script, ExecutionEntity execution)
    {
        if (ExpressionManager != null && !string.IsNullOrEmpty(ScriptFormat))
        {
            return ExpressionManager.Evaluate(script, execution.Variables);
        }

        if (script.StartsWith("${") && script.EndsWith("}"))
        {
            var varName = script.Substring(2, script.Length - 3);
            return execution.GetVariable(varName);
        }

        return null;
    }

    protected virtual void StoreScriptVariables(ExecutionEntity execution)
    {
        execution.SetVariable("_autoStoredVariables", true);
        execution.SetVariable("_scriptTimestamp", AbpTimeIdProvider.UtcNow);
    }
}

public class BusinessRuleTaskActivityBehavior : FlowNodeActivityBehavior
{
    private const string DefaultResultVariableName = "org.AsterERP.Workflow.engine.rules.OUTPUT";
    private readonly IExpressionManager? _expressionManager;
    private readonly IReadOnlyList<string> _ruleVariableInputs;
    private readonly IReadOnlyList<string> _rules;
    private readonly string _resultVariableName;
    private readonly bool _exclude;

    public BusinessRuleTaskActivityBehavior(
        string? ruleVariablesInput = null,
        string? rules = null,
        string? resultVariable = null,
        bool exclude = false,
        IExpressionManager? expressionManager = null)
    {
        _ruleVariableInputs = SplitCsv(ruleVariablesInput);
        _rules = SplitCsv(rules);
        _resultVariableName = string.IsNullOrWhiteSpace(resultVariable)
            ? DefaultResultVariableName
            : resultVariable.Trim();
        _exclude = exclude;
        _expressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var inputSnapshot = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var inputName in _ruleVariableInputs)
        {
            inputSnapshot[inputName] = execution.GetVariable(inputName);
        }

        var matches = _rules.Count == 0 || _rules.All(rule => EvaluateRule(rule, execution));
        var output = _exclude ? !matches : matches;

        execution.SetVariable(_resultVariableName, output);
        execution.SetVariableLocal("_businessRuleEvaluated", true);
        if (inputSnapshot.Count > 0)
        {
            execution.SetVariableLocal("_businessRuleInputs", inputSnapshot);
        }

        await LeaveAsync(execution, cancellationToken);
    }

    private bool EvaluateRule(string rule, ExecutionEntity execution)
    {
        if (string.IsNullOrWhiteSpace(rule))
        {
            return true;
        }

        if ((rule.StartsWith("${", StringComparison.Ordinal) || rule.StartsWith("#{", StringComparison.Ordinal)) &&
            rule.EndsWith("}", StringComparison.Ordinal))
        {
            if (_expressionManager == null)
            {
                throw new WorkflowEngineException("BusinessRuleTask requires expression manager for expression-based rules.");
            }

            return _expressionManager.EvaluateBooleanExpression(rule, execution.Variables);
        }

        if (execution.Variables.TryGetValue(rule, out var variableValue))
        {
            if (variableValue is bool boolValue)
            {
                return boolValue;
            }

            if (variableValue is string text &&
                bool.TryParse(text, out var parsedFromString))
            {
                return parsedFromString;
            }
        }

        if (bool.TryParse(rule, out var parsedRuleLiteral))
        {
            return parsedRuleLiteral;
        }

        return false;
    }

    private static IReadOnlyList<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return csv
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

public class BoundaryEventActivityBehavior : FlowNodeActivityBehavior
{
    public bool CancelActivity { get; set; } = true;
    public bool Interrupting { get; set; } = true;

    public BoundaryEventActivityBehavior() { }

    public BoundaryEventActivityBehavior(bool interrupting)
    {
        Interrupting = interrupting;
        CancelActivity = interrupting;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (Interrupting)
        {
            await ExecuteInterruptingBehaviorAsync(execution, cancellationToken);
        }
        else
        {
            await ExecuteNonInterruptingBehaviorAsync(execution, cancellationToken);
        }
    }

    protected virtual async Task ExecuteInterruptingBehaviorAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var attachedRefScopeExecution = execution.Parent != null
            ? FindParentById(execution, execution.ParentId)
            : null;

        var parentScopeExecution = FindParentScopeExecution(attachedRefScopeExecution ?? execution);

        if (parentScopeExecution != null && attachedRefScopeExecution != null)
        {
            PropagateBoundaryVariables(execution, parentScopeExecution);
            DeleteChildExecutions(attachedRefScopeExecution, execution);
            execution.Parent = parentScopeExecution;
            execution.ParentId = parentScopeExecution.Id;
        }

        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual async Task ExecuteNonInterruptingBehaviorAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var parentExecution = execution.Parent;
        var scopeExecution = FindParentScopeExecution(parentExecution ?? execution);

        if (scopeExecution != null)
        {
            var nonInterruptingExecution = new ExecutionEntity
            {
                Id = AbpTimeIdProvider.NewGuid(),
                ProcessInstanceId = scopeExecution.ProcessInstanceId,
                ProcessDefinitionId = scopeExecution.ProcessDefinitionId,
                ParentId = scopeExecution.Id,
                Parent = scopeExecution,
                IsActive = true,
                IsScope = false,
                IsConcurrent = true,
                CurrentFlowElement = execution.CurrentFlowElement,
                CurrentFlowElementId = execution.CurrentFlowElementId,
                ActivityId = execution.ActivityId,
                TenantId = scopeExecution.TenantId,
                BusinessKey = scopeExecution.BusinessKey,
                Process = scopeExecution.Process,
                Variables = new Dictionary<string, object?>()
            };
            scopeExecution.ChildExecutions.Add(nonInterruptingExecution);
            await LeaveAsync(nonInterruptingExecution, cancellationToken);
        }
        else
        {
            await LeaveAsync(execution, cancellationToken);
        }
    }

    protected virtual void PropagateBoundaryVariables(ExecutionEntity sourceExecution, ExecutionEntity targetExecution)
    {
        foreach (var variable in sourceExecution.Variables)
        {
            if (string.IsNullOrEmpty(variable.Key) || variable.Key.StartsWith("_", StringComparison.Ordinal))
            {
                continue;
            }

            targetExecution.SetVariable(variable.Key, variable.Value);
        }
    }

    protected virtual ExecutionEntity? FindParentScopeExecution(ExecutionEntity execution)
    {
        var current = execution;
        while (current != null)
        {
            if (current.IsScope)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    protected virtual ExecutionEntity? FindParentById(ExecutionEntity execution, string? parentId)
    {
        if (parentId == null) return null;
        var current = execution.Parent;
        while (current != null)
        {
            if (current.Id == parentId)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    protected virtual void DeleteChildExecutions(ExecutionEntity parentExecution, ExecutionEntity notToDeleteExecution)
    {
        foreach (var childExecution in parentExecution.ChildExecutions.ToList())
        {
            if (childExecution.Id != notToDeleteExecution.Id)
            {
                DeleteChildExecutions(childExecution, notToDeleteExecution);
                childExecution.IsActive = false;
                childExecution.IsEnded = true;
            }
        }
    }
}

public class IntermediateThrowEventActivityBehavior : FlowNodeActivityBehavior
{
    public override async global::System.Threading.Tasks.Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}


