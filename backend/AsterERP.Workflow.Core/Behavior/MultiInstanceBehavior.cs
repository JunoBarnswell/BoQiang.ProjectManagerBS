using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public abstract class MultiInstanceActivityBehavior : FlowNodeActivityBehavior
{
    public const string NumberOfInstances = "nrOfInstances";
    public const string NumberOfActiveInstances = "nrOfActiveInstances";
    public const string NumberOfCompletedInstances = "nrOfCompletedInstances";

    public IBpmnActivityBehavior? InnerActivityBehavior { get; set; }
    public IExpressionManager? ExpressionManager { get; set; }
    public BpmnModelNs.Activity? Activity { get; set; }
    public string? LoopCardinalityExpression { get; set; }
    public string? CompletionConditionExpression { get; set; }
    public string? CollectionExpression { get; set; }
    public string? CollectionVariable { get; set; }
    public string? CollectionElementVariable { get; set; }
    public string CollectionElementIndexVariable { get; set; } = "loopCounter";
    public string? LoopDataOutputRef { get; set; }
    public string? OutputDataItem { get; set; }

    protected MultiInstanceActivityBehavior() { }

    protected MultiInstanceActivityBehavior(
        BpmnModelNs.Activity activity,
        IBpmnActivityBehavior innerActivityBehavior,
        IExpressionManager? expressionManager = null)
    {
        Activity = activity;
        InnerActivityBehavior = innerActivityBehavior;
        ExpressionManager = expressionManager;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        if (execution.GetVariableLocal(CollectionElementIndexVariable) == null)
        {
            ClearLoopDataOutputRef(execution);

            var nrOfInstances = await CreateInstancesAsync(execution, cancellationToken);
            if (nrOfInstances == 0)
            {
                await LeaveAsync(execution, cancellationToken);
            }
        }
        else
        {
            if (InnerActivityBehavior != null)
            {
                await InnerActivityBehavior.ExecuteAsync(execution, cancellationToken);
            }
        }
    }

    public void ClearLoopDataOutputRef(ExecutionEntity execution)
    {
        if (HasLoopDataOutputRef())
        {
            execution.SetVariableLocal(LoopDataOutputRef!, new List<object>());
        }
    }

    protected abstract Task<int> CreateInstancesAsync(ExecutionEntity execution, CancellationToken cancellationToken);

    public int ResolveNrOfInstances(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(LoopCardinalityExpression) && ExpressionManager != null)
        {
            return ResolveLoopCardinality(execution);
        }

        if (UsesCollection())
        {
            var collection = ResolveAndValidateCollection(execution);
            if (collection is ICollection<object> col)
            {
                return col.Count;
            }
            if (collection is System.Collections.ICollection iCol)
            {
                return iCol.Count;
            }
        }

        throw new InvalidOperationException("Could not resolve number of instances: no loop cardinality or collection specified");
    }

    public int ResolveLoopCardinality(ExecutionEntity execution)
    {
        if (ExpressionManager == null || string.IsNullOrEmpty(LoopCardinalityExpression))
        {
            return 0;
        }

        var value = ExpressionManager.Evaluate(LoopCardinalityExpression, execution.Variables);
        if (value is int intValue) return intValue;
        if (value is long longValue) return (int)longValue;
        if (value is double doubleValue) return (int)doubleValue;
        if (value is short shortValue) return shortValue;
        if (value is decimal decValue) return (int)decValue;
        if (value is string stringValue && int.TryParse(stringValue, out var parsed)) return parsed;

        throw new InvalidOperationException($"Could not resolve loopCardinality expression '{LoopCardinalityExpression}': not a number");
    }

    public bool CompletionConditionSatisfied(ExecutionEntity execution)
    {
        if (ExpressionManager == null || string.IsNullOrEmpty(CompletionConditionExpression))
        {
            return false;
        }

        var expression = CompletionConditionExpression;
        if (expression.StartsWith("${") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }
        else if (expression.StartsWith("#{") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }

        var value = ExpressionManager.Evaluate(expression, execution.Variables);
        if (value is bool boolValue) return boolValue;
        if (value != null && bool.TryParse(value.ToString(), out var parsedBool)) return parsedBool;

        return false;
    }

    protected bool UsesCollection()
    {
        return !string.IsNullOrEmpty(CollectionExpression) || !string.IsNullOrEmpty(CollectionVariable);
    }

    protected object? ResolveCollection(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(CollectionExpression) && ExpressionManager != null)
        {
            return ExpressionManager.Evaluate(CollectionExpression, execution.Variables);
        }

        if (!string.IsNullOrEmpty(CollectionVariable))
        {
            return execution.GetVariable(CollectionVariable);
        }

        return null;
    }

    protected object ResolveAndValidateCollection(ExecutionEntity execution)
    {
        var obj = ResolveCollection(execution);

        if (!string.IsNullOrEmpty(CollectionExpression))
        {
            if (obj == null)
            {
                throw new InvalidOperationException($"Collection expression '{CollectionExpression}' resolved to null");
            }
            if (obj is not System.Collections.IEnumerable)
            {
                throw new InvalidOperationException($"Collection expression '{CollectionExpression}' didn't resolve to a collection");
            }
        }
        else if (!string.IsNullOrEmpty(CollectionVariable))
        {
            if (obj == null)
            {
                throw new InvalidOperationException($"Variable '{CollectionVariable}' is not found or is null");
            }
            if (obj is not System.Collections.IEnumerable)
            {
                throw new InvalidOperationException($"Variable '{CollectionVariable}' is not a collection");
            }
        }

        return obj;
    }

    public void SetLoopVariable(ExecutionEntity execution, string variableName, object? value)
    {
        execution.SetVariableLocal(variableName, value);
    }

    public int GetLoopVariable(ExecutionEntity execution, string variableName)
    {
        var value = execution.GetVariableLocal(variableName);
        var current = execution.Parent;
        while (value == null && current != null)
        {
            value = current.GetVariableLocal(variableName);
            current = current.Parent;
        }
        return value != null ? Convert.ToInt32(value) : 0;
    }

    public int GetLocalLoopVariable(ExecutionEntity execution, string variableName)
    {
        var value = execution.GetVariableLocal(variableName);
        return value != null ? Convert.ToInt32(value) : 0;
    }

    public void RemoveLocalLoopVariable(ExecutionEntity execution, string variableName)
    {
        execution.Variables.Remove(variableName);
    }

    public bool IsCompleted(ExecutionEntity execution, int completedInstances, int totalInstances)
    {
        return completedInstances >= totalInstances || CompletionConditionSatisfied(execution);
    }

    protected abstract int GetTotalInstances(ExecutionEntity execution);

    protected async Task ExecuteOriginalBehaviorAsync(ExecutionEntity execution, int loopCounter, CancellationToken cancellationToken)
    {
        if (UsesCollection() && !string.IsNullOrEmpty(CollectionElementVariable))
        {
            var collection = ResolveCollection(execution);
            if (collection is System.Collections.IEnumerable enumerable)
            {
                var index = 0;
                foreach (var item in enumerable)
                {
                    if (index == loopCounter)
                    {
                        SetLoopVariable(execution, CollectionElementVariable!, item);
                        break;
                    }
                    index++;
                }
            }
        }

        if (Activity != null)
        {
            execution.CurrentFlowElement = Activity;
            execution.CurrentFlowElementId = Activity.Id;
            execution.ActivityId = Activity.Id;
        }

        if (InnerActivityBehavior != null)
        {
            await InnerActivityBehavior.ExecuteAsync(execution, cancellationToken);
        }
    }

    public bool HasLoopDataOutputRef()
    {
        return !string.IsNullOrWhiteSpace(LoopDataOutputRef);
    }

    public bool HasOutputDataItem()
    {
        return !string.IsNullOrWhiteSpace(OutputDataItem);
    }

    public void UpdateResultCollection(ExecutionEntity childExecution, ExecutionEntity miRootExecution)
    {
        if (miRootExecution != null && HasLoopDataOutputRef())
        {
            var loopDataOutputReference = miRootExecution.GetVariableLocal(LoopDataOutputRef);
            List<object> resultCollection;
            if (loopDataOutputReference is List<object> existingList)
            {
                resultCollection = existingList;
            }
            else
            {
                resultCollection = new List<object>();
            }

            resultCollection.Add(GetResultElementItem(childExecution));
            SetLoopVariable(miRootExecution, LoopDataOutputRef!, resultCollection);
        }
    }

    public object GetResultElementItem(ExecutionEntity childExecution)
    {
        return GetResultElementItem(childExecution.Variables);
    }

    public object GetResultElementItem(Dictionary<string, object?> availableVariables)
    {
        if (HasOutputDataItem())
        {
            return availableVariables.GetValueOrDefault(OutputDataItem);
        }

        var exclusions = new HashSet<string?>
        {
            LoopDataOutputRef,
            CollectionElementIndexVariable,
            NumberOfInstances,
            NumberOfCompletedInstances,
            NumberOfActiveInstances
        };

        var resultItem = new Dictionary<string, object?>();
        foreach (var kv in availableVariables)
        {
            if (!exclusions.Contains(kv.Key))
            {
                resultItem[kv.Key] = kv.Value;
            }
        }
        return resultItem;
    }

    protected void PropagateLoopDataOutputRefToProcessInstance(ExecutionEntity miRootExecution)
    {
        if (HasLoopDataOutputRef() && miRootExecution.ProcessInstanceId != null)
        {
            var rootValue = miRootExecution.GetVariableLocal(LoopDataOutputRef);
            if (rootValue != null)
            {
                miRootExecution.SetVariable(LoopDataOutputRef!, rootValue);
            }
        }
    }

    public virtual async Task LastExecutionEndedAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task CompletingAsync(ExecutionEntity execution, ExecutionEntity? subProcessInstance, CancellationToken cancellationToken = default)
    {
        var miRootExecution = GetMultiInstanceRootExecution(execution);
        if (miRootExecution != null)
        {
            var nrOfCompletedInstances = GetLoopVariable(miRootExecution, NumberOfCompletedInstances) + 1;
            SetLoopVariable(miRootExecution, NumberOfCompletedInstances, nrOfCompletedInstances);

            var nrOfActiveInstances = GetLoopVariable(miRootExecution, NumberOfActiveInstances);
            if (nrOfActiveInstances > 0)
            {
                SetLoopVariable(miRootExecution, NumberOfActiveInstances, nrOfActiveInstances - 1);
            }

            if (subProcessInstance != null)
            {
                UpdateResultCollection(subProcessInstance, miRootExecution);
            }
        }

        await Task.CompletedTask;
    }

    protected virtual ExecutionEntity? GetMultiInstanceRootExecution(ExecutionEntity execution)
    {
        var current = execution;
        while (current != null)
        {
            if (current.GetVariableLocal(NumberOfInstances) != null)
            {
                return current;
            }
            current = current.Parent;
        }
        return null;
    }

    public virtual async Task CompletedAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? signalName, object? signalData, CancellationToken cancellationToken = default)
    {
        if (InnerActivityBehavior is ITriggerableActivityBehavior triggerable)
        {
            await triggerable.TriggerAsync(execution, signalName, signalData, cancellationToken);
        }
    }
}

public class ParallelMultiInstanceActivityBehavior : MultiInstanceActivityBehavior
{
    public ParallelMultiInstanceActivityBehavior() { }

    public ParallelMultiInstanceActivityBehavior(
        BpmnModelNs.Activity activity,
        IBpmnActivityBehavior innerActivityBehavior,
        IExpressionManager? expressionManager = null)
        : base(activity, innerActivityBehavior, expressionManager) { }

    protected override async Task<int> CreateInstancesAsync(ExecutionEntity execution, CancellationToken cancellationToken)
    {
        var nrOfInstances = ResolveNrOfInstances(execution);
        if (nrOfInstances < 0)
        {
            throw new InvalidOperationException($"Invalid number of instances: must be non-negative integer value, but was {nrOfInstances}");
        }

        SetLoopVariable(execution, NumberOfInstances, nrOfInstances);
        SetLoopVariable(execution, NumberOfCompletedInstances, 0);
        SetLoopVariable(execution, NumberOfActiveInstances, nrOfInstances);

        var concurrentExecutions = new List<ExecutionEntity>();
        for (var loopCounter = 0; loopCounter < nrOfInstances; loopCounter++)
        {
            var childExecution = new ExecutionEntity
            {
                Id = AbpTimeIdProvider.NewGuid(),
                ProcessInstanceId = execution.ProcessInstanceId,
                ProcessDefinitionId = execution.ProcessDefinitionId,
                ParentId = execution.Id,
                Parent = execution,
                IsActive = true,
                IsScope = false,
                IsConcurrent = true,
                CurrentFlowElement = Activity,
                CurrentFlowElementId = Activity?.Id,
                ActivityId = Activity?.Id,
                TenantId = execution.TenantId,
                Variables = new Dictionary<string, object?>()
            };
            execution.ChildExecutions.Add(childExecution);
            concurrentExecutions.Add(childExecution);
        }

        for (var loopCounter = 0; loopCounter < nrOfInstances; loopCounter++)
        {
            var concurrentExecution = concurrentExecutions[loopCounter];
            if (concurrentExecution.IsActive && !concurrentExecution.IsEnded)
            {
                SetLoopVariable(concurrentExecution, CollectionElementIndexVariable, loopCounter);
                await ExecuteOriginalBehaviorAsync(concurrentExecution, loopCounter, cancellationToken);
            }
        }

        if (concurrentExecutions.Count > 0)
        {
            execution.IsActive = false;
        }

        return nrOfInstances;
    }

    public virtual async Task LeaveInstanceAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var nrOfInstances = GetLoopVariable(execution, NumberOfInstances);
        var nrOfCompletedInstances = GetLoopVariable(execution, NumberOfCompletedInstances) + 1;
        var nrOfActiveInstances = GetLoopVariable(execution, NumberOfActiveInstances) - 1;

        var miRootExecution = execution.Parent;
        if (miRootExecution != null)
        {
            SetLoopVariable(miRootExecution, NumberOfCompletedInstances, nrOfCompletedInstances);
            SetLoopVariable(miRootExecution, NumberOfActiveInstances, nrOfActiveInstances);
        }

        UpdateResultCollection(execution, miRootExecution ?? execution);
        execution.IsActive = false;
        execution.IsEnded = true;

        if (nrOfCompletedInstances >= nrOfInstances || (miRootExecution != null && CompletionConditionSatisfied(miRootExecution)))
        {
            var executionToUse = miRootExecution ?? execution;
            executionToUse.IsActive = true;

            if (HasLoopDataOutputRef())
            {
                PropagateLoopDataOutputRefToProcessInstance(executionToUse);
            }

            RemoveLocalLoopVariable(executionToUse, CollectionElementIndexVariable);
            await LeaveAsync(executionToUse, cancellationToken);
        }
        else
        {
            execution.IsActive = false;
        }
    }

    protected override int GetTotalInstances(ExecutionEntity execution)
    {
        return GetLoopVariable(execution, NumberOfInstances);
    }
}

public class SequentialMultiInstanceActivityBehavior : MultiInstanceActivityBehavior
{
    public SequentialMultiInstanceActivityBehavior() { }

    public SequentialMultiInstanceActivityBehavior(
        BpmnModelNs.Activity activity,
        IBpmnActivityBehavior innerActivityBehavior,
        IExpressionManager? expressionManager = null)
        : base(activity, innerActivityBehavior, expressionManager) { }

    protected override async Task<int> CreateInstancesAsync(ExecutionEntity execution, CancellationToken cancellationToken)
    {
        var nrOfInstances = ResolveNrOfInstances(execution);
        if (nrOfInstances < 0)
        {
            throw new InvalidOperationException($"Invalid number of instances: must be non-negative integer value, but was {nrOfInstances}");
        }

        if (nrOfInstances == 0)
        {
            return 0;
        }

        var childExecution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessInstanceId = execution.ProcessInstanceId,
            ProcessDefinitionId = execution.ProcessDefinitionId,
            ParentId = execution.Id,
            Parent = execution,
            IsActive = true,
            IsScope = false,
            CurrentFlowElement = Activity,
            CurrentFlowElementId = Activity?.Id,
            ActivityId = Activity?.Id,
            TenantId = execution.TenantId,
            Variables = new Dictionary<string, object?>()
        };
        execution.ChildExecutions.Add(childExecution);

        SetLoopVariable(execution, NumberOfInstances, nrOfInstances);
        SetLoopVariable(execution, NumberOfCompletedInstances, 0);
        SetLoopVariable(execution, NumberOfActiveInstances, 1);
        SetLoopVariable(childExecution, CollectionElementIndexVariable, 0);

        execution.IsActive = false;

        await ExecuteOriginalBehaviorAsync(childExecution, 0, cancellationToken);
        return nrOfInstances;
    }

    public virtual async Task LeaveInstanceAsync(ExecutionEntity childExecution, CancellationToken cancellationToken = default)
    {
        var miRootExecution = childExecution.Parent;
        if (miRootExecution == null)
        {
            await LeaveAsync(childExecution, cancellationToken);
            return;
        }

        var nrOfInstances = GetLoopVariable(miRootExecution, NumberOfInstances);
        var loopCounter = GetLoopVariable(childExecution, CollectionElementIndexVariable) + 1;
        var nrOfCompletedInstances = GetLoopVariable(miRootExecution, NumberOfCompletedInstances) + 1;

        SetLoopVariable(miRootExecution, NumberOfCompletedInstances, nrOfCompletedInstances);
        SetLoopVariable(childExecution, CollectionElementIndexVariable, loopCounter);

        UpdateResultCollection(childExecution, miRootExecution);

        if (loopCounter >= nrOfInstances || CompletionConditionSatisfied(miRootExecution))
        {
            if (HasLoopDataOutputRef())
            {
                PropagateLoopDataOutputRefToProcessInstance(miRootExecution);
            }

            RemoveLocalLoopVariable(childExecution, CollectionElementIndexVariable);
            childExecution.IsActive = false;
            childExecution.IsEnded = true;
            miRootExecution.IsActive = true;
            await LeaveAsync(miRootExecution, cancellationToken);
        }
        else
        {
            await ExecuteOriginalBehaviorAsync(childExecution, loopCounter, cancellationToken);
        }
    }

    protected override int GetTotalInstances(ExecutionEntity execution)
    {
        return GetLoopVariable(execution, NumberOfInstances);
    }
}


