using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Service;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Cmd;

public class ProcessInstanceResult
{
    public string Id { get; init; } = null!;
    public string? ProcessDefinitionId { get; init; }
    public string? ProcessDefinitionKey { get; init; }
    public string? BusinessKey { get; init; }
    public string? ProcessInstanceId { get; init; }
    public bool IsStarted { get; init; }
    public bool IsEnded { get; init; }
    public string? TenantId { get; init; }
    public string? Name { get; init; }
    public ExecutionEntity? Execution { get; init; }
}

public class StartProcessInstanceCmd : ICommand<ProcessInstanceResult>
{
    protected readonly string? _processDefinitionKey;
    protected readonly string? _processDefinitionId;
    protected readonly string? _businessKey;
    protected readonly Dictionary<string, object?>? _variables;
    protected readonly string? _tenantId;
    protected readonly string? _processInstanceName;
    protected readonly ExecutionEntity? _superExecution;

    public StartProcessInstanceCmd(
        string? processDefinitionKey,
        string? processDefinitionId,
        string? businessKey,
        Dictionary<string, object?>? variables)
    {
        _processDefinitionKey = processDefinitionKey;
        _processDefinitionId = processDefinitionId;
        _businessKey = businessKey;
        _variables = variables;
    }

    public StartProcessInstanceCmd(
        string? processDefinitionKey,
        string? processDefinitionId,
        string? businessKey,
        Dictionary<string, object?>? variables,
        string? tenantId) : this(processDefinitionKey, processDefinitionId, businessKey, variables)
    {
        _tenantId = tenantId;
    }

    public StartProcessInstanceCmd(
        string? processDefinitionKey,
        string? processDefinitionId,
        string? businessKey,
        Dictionary<string, object?>? variables,
        string? tenantId,
        ExecutionEntity? superExecution) : this(processDefinitionKey, processDefinitionId, businessKey, variables, tenantId)
    {
        _superExecution = superExecution;
    }


    public async Task<ProcessInstanceResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var processDefinition = ResolveProcessDefinition(context);
        var process = ResolveProcess(context, processDefinition);
        var execution = CreateProcessInstanceExecution(context, processDefinition);

        if (process != null)
        {
            EnsureBehaviorsBound(context, process);
            SetInitialFlowElement(execution, process);
        }

        if (_variables != null)
        {
            foreach (var kvp in _variables)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        execution.BusinessKey = _businessKey;
        context.AddExecution(execution);

        if (_variables != null)
        {
            foreach (var kvp in _variables)
            {
                context.ProcessEngineConfiguration.HistoryManager.RecordVariable(
                    execution,
                    kvp.Key,
                    kvp.Value,
                    taskId: null);
            }
        }
        if (_superExecution != null && !_superExecution.ChildExecutions.Any(child => child.Id == execution.Id))
        {
            _superExecution.ChildExecutions.Add(execution);
        }

        context.ProcessEngineConfiguration.HistoryManager.RecordProcessInstanceStart(execution);

        DispatchProcessStartedEvent(context, execution);

        if (execution.CurrentFlowElement != null)
        {
            var agenda = new WorkflowEngineAgendaFactory(context.ProcessEngineConfiguration).CreateAgenda();
            agenda.PlanContinueProcessOperation(execution);
            while (!agenda.IsEmpty)
            {
                await agenda.ExecuteNextAsync(cancellationToken);
            }
        }

        return new ProcessInstanceResult
        {
            Id = execution.Id,
            ProcessDefinitionId = processDefinition.Id,
            ProcessDefinitionKey = processDefinition.Key,
            BusinessKey = _businessKey,
            ProcessInstanceId = execution.ProcessInstanceId ?? execution.Id,
            IsStarted = true,
            IsEnded = execution.IsEnded,
            TenantId = _tenantId,
            Name = _processInstanceName,
            Execution = execution
        };
    }

    protected virtual Services.ProcessDefinitionRecord ResolveProcessDefinition(ICommandContext context)
    {
        if (!string.IsNullOrEmpty(_processDefinitionId))
        {
            var def = context.ProcessEngineConfiguration.CommandExecutor.Execute(
                new GetProcessDefinitionByIdCmd(_processDefinitionId));
            if (def != null) return def;
        }

        if (!string.IsNullOrEmpty(_processDefinitionKey))
        {
            var defs = context.ProcessEngineConfiguration.CommandExecutor.Execute(
                new GetProcessDefinitionsCmd());
            var latest = defs
                .Where(d => d.Key == _processDefinitionKey)
                .OrderByDescending(d => d.Version)
                .ThenByDescending(d => d.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (latest != null) return latest;
        }

        throw new WorkflowEngineObjectNotFoundException(
            $"No process definition found for key '{_processDefinitionKey}' or id '{_processDefinitionId}'");
    }

    protected virtual ExecutionEntity CreateProcessInstanceExecution(
        ICommandContext context, Services.ProcessDefinitionRecord processDefinition)
    {
        var id = $"process-instance-{AbpTimeIdProvider.NewGuid("N")}";
        return new ExecutionEntity
        {
            Id = id,
            ProcessDefinitionId = processDefinition.Id,
            ProcessInstanceId = id,
            Parent = _superExecution,
            ParentId = _superExecution?.Id,
            SuperExecutionId = _superExecution?.Id,
            IsActive = true,
            IsEnded = false,
            IsScope = true,
            IsConcurrent = false,
            IsProcessInstanceType = true,
            Variables = new Dictionary<string, object?>()
        };
    }

    protected virtual BpmnModelNs.Process? ResolveProcess(
        ICommandContext context,
        Services.ProcessDefinitionRecord processDefinition)
    {
        var modelId = processDefinition.BpmnModelId ?? processDefinition.Id;
        var bpmnModel = ProcessDefinitionHelperImpl.ResolveBpmnModel(
            context.ProcessEngineConfiguration,
            modelId);

        if (bpmnModel == null || bpmnModel.Processes.Count == 0)
            return null;

        return !string.IsNullOrEmpty(processDefinition.Key)
            ? bpmnModel.GetProcessById(processDefinition.Key) ?? bpmnModel.Processes.FirstOrDefault()
            : bpmnModel.GetProcessById(processDefinition.Id) ?? bpmnModel.Processes.FirstOrDefault();
    }

    protected virtual void EnsureBehaviorsBound(ICommandContext context, BpmnModelNs.Process process)
    {
        if (ProcessHasBoundBehaviors(process))
        {
            return;
        }

        var behaviorFactory = new DefaultActivityBehaviorFactory(
            EmptyServiceProvider.Instance,
            context.ProcessEngineConfiguration.ExpressionManager);
        BpmnBehaviorBinder.BindBehaviors(process, behaviorFactory);
    }

    private static bool ProcessHasBoundBehaviors(BpmnModelNs.Process process)
    {
        return FlattenFlowElements(process)
            .OfType<BpmnModelNs.FlowNode>()
            .All(flowNode => flowNode.Behavior != null);
    }

    private static IEnumerable<BpmnModelNs.FlowElement> FlattenFlowElements(BpmnModelNs.IFlowElementsContainer container)
    {
        foreach (var flowElement in container.FlowElements)
        {
            yield return flowElement;

            if (flowElement is BpmnModelNs.SubProcess subProcess)
            {
                foreach (var child in FlattenFlowElements(subProcess))
                {
                    yield return child;
                }
            }
        }
    }

    protected virtual void SetInitialFlowElement(ExecutionEntity execution, BpmnModelNs.Process process)
    {
        var initialFlowElement = process.GetInitialFlowElement()
            ?? throw new WorkflowEngineException($"No start event found in process '{process.Id}'");

        execution.Process = process;
        execution.CurrentFlowElement = initialFlowElement;
        execution.CurrentFlowElementId = initialFlowElement.Id;
        execution.ActivityId = initialFlowElement.Id;
    }

    protected virtual void DispatchProcessStartedEvent(ICommandContext context, ExecutionEntity execution)
    {
        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateProcessStartedEvent(
                    execution.ProcessInstanceId ?? execution.Id,
                    execution.ProcessDefinitionId ?? "",
                    execution.BusinessKey));
        }
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        private EmptyServiceProvider()
        {
        }

        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}

