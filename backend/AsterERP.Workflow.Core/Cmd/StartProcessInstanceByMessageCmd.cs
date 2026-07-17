using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Cmd;

public class StartProcessInstanceByMessageCmd : ICommand<ProcessInstanceResult>
{
    private readonly string _messageName;
    private readonly string? _businessKey;
    private readonly Dictionary<string, object?>? _variables;
    private readonly string? _tenantId;

    public StartProcessInstanceByMessageCmd(
        string messageName,
        string? businessKey,
        Dictionary<string, object?>? variables)
    {
        _messageName = messageName ?? throw new ArgumentNullException(nameof(messageName));
        _businessKey = businessKey;
        _variables = variables;
    }

    public StartProcessInstanceByMessageCmd(
        string messageName,
        string? businessKey,
        Dictionary<string, object?>? variables,
        string? tenantId) : this(messageName, businessKey, variables)
    {
        _tenantId = tenantId;
    }

    public ProcessInstanceResult Execute(ICommandContext context)
    {
        var processDefinition = FindProcessDefinitionByMessage(context);

        var execution = new ExecutionEntity
        {
            Id = AbpTimeIdProvider.NewGuid("N"),
            ProcessDefinitionId = processDefinition.Id,
            ProcessInstanceId = AbpTimeIdProvider.NewGuid("N"),
            IsActive = true,
            IsEnded = false,
            IsScope = true,
            IsConcurrent = false,
            IsProcessInstanceType = true,
            BusinessKey = _businessKey,
            Variables = new Dictionary<string, object?>()
        };

        if (_variables != null)
        {
            foreach (var kvp in _variables)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        context.AddExecution(execution);

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            eventDispatcher.DispatchEvent(
                WorkflowEventBuilder.CreateProcessStartedEvent(
                    execution.ProcessInstanceId ?? execution.Id,
                    execution.ProcessDefinitionId ?? "",
                    execution.BusinessKey));
        }

        return new ProcessInstanceResult
        {
            Id = execution.Id,
            ProcessDefinitionId = processDefinition.Id,
            ProcessDefinitionKey = processDefinition.Key,
            BusinessKey = _businessKey,
            ProcessInstanceId = execution.ProcessInstanceId ?? execution.Id,
            IsStarted = true,
            IsEnded = false,
            TenantId = _tenantId
        };
    }

    public async Task<ProcessInstanceResult> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        return Execute(context);
    }

    private Services.ProcessDefinitionRecord FindProcessDefinitionByMessage(ICommandContext context)
    {
        var defs = context.ProcessEngineConfiguration.CommandExecutor.Execute(
            new GetProcessDefinitionsCmd());

        var matched = defs.FirstOrDefault(definition =>
            IsMessageStartDefinition(context, definition, _messageName, _tenantId));

        if (matched == null)
            throw new WorkflowEngineObjectNotFoundException(
                $"No process definition found for message '{_messageName}'");

        return matched;
    }

    private static bool IsMessageStartDefinition(
        ICommandContext context,
        Services.ProcessDefinitionRecord definition,
        string messageName,
        string? tenantId)
    {
        if (!string.IsNullOrWhiteSpace(tenantId) &&
            !string.Equals(definition.TenantId, tenantId, StringComparison.Ordinal))
        {
            return false;
        }

        var deploymentId = definition.DeploymentId;
        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            return false;
        }

        var deploymentManager = context.ProcessEngineConfiguration.DeploymentManager;
        if (deploymentManager == null)
        {
            return false;
        }

        var cacheEntry = deploymentManager.ResolveProcessDefinition(definition.Id);
        var bpmnModel = cacheEntry?.BpmnModel;
        if (bpmnModel == null)
        {
            return false;
        }

        foreach (var process in bpmnModel.Processes)
        {
            foreach (var startEvent in process.FlowElements.OfType<StartEvent>())
            {
                if (startEvent.EventDefinitions == null)
                {
                    continue;
                }

                foreach (var eventDefinition in startEvent.EventDefinitions.OfType<MessageEventDefinition>())
                {
                    var resolvedMessageName = ResolveMessageName(bpmnModel, eventDefinition);
                    if (string.Equals(resolvedMessageName, messageName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static string? ResolveMessageName(BpmnModel.BpmnModel bpmnModel, MessageEventDefinition eventDefinition)
    {
        if (string.IsNullOrWhiteSpace(eventDefinition.MessageRef))
        {
            return null;
        }

        if (!bpmnModel.MessageMap.TryGetValue(eventDefinition.MessageRef, out var message))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(message.Name) ? null : message.Name;
    }
}

