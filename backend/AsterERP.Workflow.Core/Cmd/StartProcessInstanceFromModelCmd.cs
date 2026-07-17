using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Behavior;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Cmd;

public class StartProcessInstanceFromModelCmd : ICommand<ProcessInstanceResult>
{
    private readonly ProcessDefinitionRecord _processDefinition;
    private readonly BpmnModelNs.BpmnModel _bpmnModel;
    private readonly IActivityBehaviorFactory _behaviorFactory;
    private readonly Dictionary<string, object?>? _variables;
    private readonly string? _businessKey;
    private readonly string? _tenantId;

    public StartProcessInstanceFromModelCmd(
        ProcessDefinitionRecord processDefinition,
        BpmnModelNs.BpmnModel bpmnModel,
        IActivityBehaviorFactory behaviorFactory,
        Dictionary<string, object?>? variables = null,
        string? businessKey = null,
        string? tenantId = null)
    {
        _processDefinition = processDefinition ?? throw new ArgumentNullException(nameof(processDefinition));
        _bpmnModel = bpmnModel ?? throw new ArgumentNullException(nameof(bpmnModel));
        _behaviorFactory = behaviorFactory ?? throw new ArgumentNullException(nameof(behaviorFactory));
        _variables = variables;
        _businessKey = businessKey;
        _tenantId = tenantId;
    }


    public async Task<ProcessInstanceResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        var process = ResolveProcess();
        BpmnBehaviorBinder.BindBehaviors(process, _behaviorFactory);

        var initialFlowElement = process.GetInitialFlowElement();
        if (initialFlowElement == null)
            throw new WorkflowEngineException($"No start event found in process '{process.Id}'");

        var processInstanceId = $"process-instance-{AbpTimeIdProvider.NewGuid("N")}";
        var execution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid(),
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = _processDefinition.Id,
            CurrentFlowElement = initialFlowElement,
            CurrentFlowElementId = initialFlowElement.Id,
            ActivityId = initialFlowElement.Id,
            IsActive = true,
            IsEnded = false,
            IsScope = true,
            IsProcessInstanceType = true,
            Process = process,
            BusinessKey = _businessKey,
            TenantId = _tenantId,
            Variables = _variables != null
                ? _variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                : new Dictionary<string, object?>()
        };

        context.AddExecution(execution);

        var agenda = new WorkflowEngineAgendaFactory(context.ProcessEngineConfiguration).CreateAgenda();
        agenda.PlanContinueProcessOperation(execution);
        while (!agenda.IsEmpty)
        {
            await agenda.ExecuteNextAsync(cancellationToken);
        }

        return new ProcessInstanceResult
        {
            Id = processInstanceId,
            ProcessInstanceId = processInstanceId,
            ProcessDefinitionId = _processDefinition.Id,
            ProcessDefinitionKey = _processDefinition.Key,
            BusinessKey = _businessKey,
            IsStarted = true,
            IsEnded = execution.IsEnded,
            TenantId = _tenantId,
            Name = _processDefinition.Name,
            Execution = execution
        };
    }

    private BpmnModelNs.Process ResolveProcess()
    {
        var process = !string.IsNullOrEmpty(_processDefinition.Key)
            ? _bpmnModel.GetProcessById(_processDefinition.Key)
            : null;

        process ??= _bpmnModel.GetProcessById(_processDefinition.Id);
        process ??= _bpmnModel.Processes.FirstOrDefault();

        return process ?? throw new WorkflowEngineException($"No process found for definition '{_processDefinition.Id}'");
    }

}


