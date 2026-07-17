using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class CallActivityBehavior : FlowNodeActivityBehavior
{
    public string? CalledElement { get; set; }
    public string? ProcessKey { get; set; }
    public bool SameDeployment { get; set; }
    public string? CalledElementBinding { get; set; }
    public int? CalledElementVersion { get; set; }
    public string? CalledElementTenantId { get; set; }
    public bool CompleteAsync { get; set; }
    public bool InheritVariables { get; set; }
    public bool InheritBusinessKey { get; set; }
    public string? BusinessKey { get; set; }
    public List<BpmnModelNs.IOParameter> InParameters { get; set; } = new();
    public List<BpmnModelNs.IOParameter> OutParameters { get; set; } = new();
    public List<BpmnModelNs.MapExceptionEntry> MapExceptions { get; set; } = new();
    public VariablesPropagator? VariablesPropagator { get; set; }

    protected IExpressionManager? ExpressionManager { get; set; }
    protected IProcessDefinitionManager? ProcessDefinitionManager { get; set; }
    protected IRuntimeService? RuntimeService { get; set; }

    public CallActivityBehavior() { }

    public CallActivityBehavior(
        string? calledElement,
        IExpressionManager? expressionManager = null,
        IProcessDefinitionManager? processDefinitionManager = null,
        IRuntimeService? runtimeService = null)
    {
        CalledElement = calledElement;
        ProcessKey = calledElement;
        ExpressionManager = expressionManager;
        ProcessDefinitionManager = processDefinitionManager;
        RuntimeService = runtimeService;
    }

    public CallActivityBehavior(
        string? calledElement,
        List<BpmnModelNs.MapExceptionEntry> mapExceptions,
        VariablesPropagator? variablesPropagator = null,
        IExpressionManager? expressionManager = null,
        IProcessDefinitionManager? processDefinitionManager = null,
        IRuntimeService? runtimeService = null)
    {
        CalledElement = calledElement;
        ProcessKey = calledElement;
        MapExceptions = mapExceptions ?? new();
        VariablesPropagator = variablesPropagator ?? new VariablesPropagator(new NoneVariablesCalculator());
        ExpressionManager = expressionManager;
        ProcessDefinitionManager = processDefinitionManager;
        RuntimeService = runtimeService;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        var processDefinitionKey = ResolveProcessDefinitionKey(execution);
        if (string.IsNullOrEmpty(processDefinitionKey))
        {
            throw new WorkflowEngineException($"Cannot start sub process. Process definition key is null for call activity {execution.CurrentActivityId}");
        }

        var processDefinition = await FindProcessDefinitionAsync(processDefinitionKey, execution.TenantId, execution, cancellationToken);
        if (processDefinition == null)
        {
            throw new WorkflowEngineException($"Cannot start a sub process instance. Process definition with key '{processDefinitionKey}' could not be found");
        }

        if (processDefinition.IsSuspended)
        {
            throw new WorkflowEngineException($"Cannot start process instance. Process definition '{processDefinition.Name}' (id = {processDefinition.Id}) is suspended");
        }

        var businessKey = ResolveBusinessKey(execution);

        var subProcessVariables = await CreateSubProcessVariablesAsync(execution, processDefinition, cancellationToken);

        var subProcessInstance = await CreateSubProcessInstanceAsync(
            execution,
            processDefinition,
            businessKey,
            subProcessVariables,
            cancellationToken);

        RecordSubProcessInstanceStart(execution, subProcessInstance);
        execution.SetVariable("_calledProcessDefinitionId", processDefinition.Id);
    }

    public virtual async Task CompletingAsync(ExecutionEntity execution, string? subProcessInstanceId, CancellationToken cancellationToken = default)
    {
        if (subProcessInstanceId != null && RuntimeService != null)
        {
            var subProcessVariables = await RuntimeService.GetVariablesAsync(subProcessInstanceId, cancellationToken: cancellationToken);
            CopyOutParameters(execution, subProcessVariables);

            if (VariablesPropagator != null)
            {
                VariablesPropagator.Propagate(execution, subProcessVariables);
            }
        }
    }

    public virtual async Task CompletedAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        await LeaveAsync(execution, cancellationToken);
    }

    protected virtual string? ResolveProcessDefinitionKey(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(CalledElement))
        {
            if (ExpressionManager != null && (CalledElement.StartsWith("${") || CalledElement.StartsWith("#{")))
            {
                var result = ExpressionManager.Evaluate(CalledElement, execution.Variables);
                return result?.ToString();
            }
            return CalledElement;
        }

        if (!string.IsNullOrEmpty(ProcessKey))
        {
            return ProcessKey;
        }

        if (ExpressionManager != null)
        {
            var flowElement = execution.CurrentFlowElement;
            if (flowElement is BpmnModelNs.CallActivity callActivity && !string.IsNullOrEmpty(callActivity.CalledElement))
            {
                return callActivity.CalledElement;
            }
        }

        return null;
    }

    protected virtual async Task<ProcessDefinition?> FindProcessDefinitionAsync(
        string processDefinitionKey,
        string? tenantId,
        ExecutionEntity? execution = null,
        CancellationToken cancellationToken = default)
    {
        if (ProcessDefinitionManager != null)
        {
            if (SameDeployment)
            {
                var deploymentId = execution?.GetVariable("_deploymentId") as string;
                if (!string.IsNullOrEmpty(deploymentId))
                {
                    return await ProcessDefinitionManager.FindDeployedProcessDefinitionByDeploymentIdAndKeyAsync(deploymentId, processDefinitionKey, tenantId, cancellationToken);
                }
            }

            if (CalledElementVersion.HasValue)
            {
                return await ProcessDefinitionManager.FindDeployedProcessDefinitionByKeyAndVersionAsync(processDefinitionKey, CalledElementVersion.Value, tenantId, cancellationToken);
            }

            return await ProcessDefinitionManager.FindDeployedLatestProcessDefinitionByKeyAsync(processDefinitionKey, tenantId, cancellationToken);
        }

        return null;
    }

    protected virtual string? ResolveBusinessKey(ExecutionEntity execution)
    {
        if (!string.IsNullOrEmpty(BusinessKey) && ExpressionManager != null)
        {
            var result = ExpressionManager.Evaluate(BusinessKey, execution.Variables);
            return result?.ToString();
        }

        if (InheritBusinessKey)
        {
            return execution.BusinessKey;
        }

        return null;
    }

    protected virtual async Task<Dictionary<string, object>> CreateSubProcessVariablesAsync(
        ExecutionEntity execution,
        ProcessDefinition processDefinition,
        CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object>();

        if (InheritVariables)
        {
            foreach (var variable in execution.Variables)
            {
                if (variable.Value != null)
                {
                    variables[variable.Key] = variable.Value;
                }
            }
        }

        var inputVariablesFromCalculator = CalculateInboundVariables(execution, processDefinition);
        foreach (var kv in inputVariablesFromCalculator)
        {
            variables[kv.Key] = kv.Value;
        }

        foreach (var inParam in InParameters)
        {
            object? value = null;
            if (!string.IsNullOrEmpty(inParam.SourceExpression) && ExpressionManager != null)
            {
                value = ExpressionManager.Evaluate(inParam.SourceExpression, execution.Variables);
            }
            else if (!string.IsNullOrEmpty(inParam.Source))
            {
                value = execution.GetVariable(inParam.Source);
            }

            if (value != null && !string.IsNullOrEmpty(inParam.Target))
            {
                variables[inParam.Target] = value;
            }
        }

        return variables;
    }

    protected virtual Dictionary<string, object> CalculateInboundVariables(
        ExecutionEntity execution,
        ProcessDefinition processDefinition)
    {
        if (VariablesPropagator?.Calculator != null)
        {
            var inputVars = VariablesPropagator.Calculator.CalculateInputVariables(execution);
            if (inputVars != null)
            {
                return new Dictionary<string, object>(inputVars);
            }
        }
        return new Dictionary<string, object>();
    }

    protected virtual async Task<ExecutionEntity> CreateSubProcessInstanceAsync(
        ExecutionEntity execution,
        ProcessDefinition processDefinition,
        string? businessKey,
        Dictionary<string, object> variables,
        CancellationToken cancellationToken)
    {
        var subProcessInstance = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessDefinitionId = processDefinition.Id,
            BusinessKey = businessKey,
            TenantId = execution.TenantId,
            IsActive = true,
            IsScope = true,
            IsProcessInstanceType = true,
            ParentId = execution.Id,
            Variables = new Dictionary<string, object?>(variables.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)))
        };

        if (RuntimeService != null)
        {
            return await RuntimeService.StartProcessInstanceAndGetExecutionAsync(processDefinition.Key!, businessKey, new Dictionary<string, object?>(variables.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value))), execution.TenantId, execution, cancellationToken);
        }

        return subProcessInstance;
    }

    protected virtual void CopyOutParameters(ExecutionEntity execution, Dictionary<string, object> subProcessVariables)
    {
        foreach (var outParam in OutParameters)
        {
            object? value = null;
            if (!string.IsNullOrEmpty(outParam.SourceExpression) && ExpressionManager != null)
            {
                value = ExpressionManager.Evaluate(outParam.SourceExpression, subProcessVariables);
            }
            else if (!string.IsNullOrEmpty(outParam.Source))
            {
                value = subProcessVariables.TryGetValue(outParam.Source, out var v) ? v : null;
            }

            if (value != null && !string.IsNullOrEmpty(outParam.Target))
            {
                execution.SetVariable(outParam.Target, value);
            }
        }
    }

    protected virtual void RecordSubProcessInstanceStart(ExecutionEntity parentExecution, ExecutionEntity subProcessInstance)
    {
        parentExecution.SetVariable("_calledProcessInstanceId", subProcessInstance.Id);
        parentExecution.SetVariable("_calledActivityId", parentExecution.CurrentActivityId);
    }
}


