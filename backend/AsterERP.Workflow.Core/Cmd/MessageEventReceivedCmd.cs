using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Services;

namespace AsterERP.Workflow.Core.Cmd;

public class MessageEventReceivedCmd : ICommand<object?>
{
    private readonly string _messageName;
    private readonly string _executionId;
    private readonly Dictionary<string, object?>? _payload;

    public MessageEventReceivedCmd(
        string messageName,
        string executionId,
        Dictionary<string, object?>? processVariables = null)
    {
        _messageName = messageName ?? throw new ArgumentNullException(nameof(messageName));
        _executionId = executionId ?? throw new ArgumentNullException(nameof(executionId));
        _payload = processVariables != null ? new Dictionary<string, object?>(processVariables) : null;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_messageName))
            throw new WorkflowEngineArgumentException("messageName is null");

        if (string.IsNullOrEmpty(_executionId))
            throw new WorkflowEngineArgumentException("executionId is null");

        var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);

        if (execution == null)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"Execution '{_executionId}' could not be found",
                typeof(Execution.ExecutionEntity));
        }

        if (execution.IsEnded)
            throw new WorkflowEngineException(
                $"Cannot throw message event '{_messageName}' because execution '{_executionId}' is ended");

        if (_payload != null)
        {
            foreach (var kvp in _payload)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        var subscriptionName = execution.GetVariableLocal("_messageSubscriptionName") as string;
        var hasMatchingSubscription =
            subscriptionName == _messageName ||
            Equals(execution.GetVariable("_messageSubscription_" + _messageName), true) ||
            HasMessageCatchSubscription(execution, _messageName);
        if (!hasMatchingSubscription)
        {
            var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
            if (store?.IsEnabled == true)
            {
                hasMatchingSubscription = await store.HasMessageSubscriptionAsync(
                    _executionId,
                    _messageName,
                    cancellationToken);
            }
        }

        if (!hasMatchingSubscription)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"Execution '{_executionId}' does not have a message subscription for '{_messageName}'");
        }

        execution.RemoveVariable("_messageSubscription_" + _messageName);
        execution.SetVariableLocal("_messageSubscriptionActive", false);
        execution.SetVariableLocal("_messageReceived_" + _messageName, true);
        execution.SetVariableLocal("_messageName", _messageName);
        execution.IsActive = true;

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            await eventDispatcher.DispatchEventAsync(
                WorkflowEventBuilder.CreateCustomEvent("MESSAGE_EVENT_RECEIVED",
                    new Dictionary<string, object?>
                    {
                        ["messageName"] = _messageName,
                        ["executionId"] = _executionId
                    }), cancellationToken);
        }

        var agenda = new WorkflowEngineAgendaFactory(context.ProcessEngineConfiguration).CreateAgenda();
        agenda.PlanTriggerExecutionOperation(execution);
        while (!agenda.IsEmpty)
        {
            await agenda.ExecuteNextAsync(cancellationToken);
        }

        return null;
    }

    private static bool HasMessageCatchSubscription(Execution.ExecutionEntity execution, string messageName)
    {
        if (execution.CurrentFlowElement is not AsterERP.Workflow.BpmnModel.CatchEvent catchEvent)
        {
            return false;
        }

        foreach (var eventDefinition in catchEvent.EventDefinitions.OfType<AsterERP.Workflow.BpmnModel.MessageEventDefinition>())
        {
            var resolvedMessageName = ResolveMessageName(execution, eventDefinition);
            if (string.Equals(resolvedMessageName, messageName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveMessageName(
        Execution.ExecutionEntity execution,
        AsterERP.Workflow.BpmnModel.MessageEventDefinition eventDefinition)
    {
        if (string.IsNullOrWhiteSpace(eventDefinition.MessageRef))
        {
            return null;
        }

        var messageRef = eventDefinition.MessageRef!;
        if (execution.Process?.BpmnModel?.MessageMap.TryGetValue(messageRef, out var message) == true)
        {
            return string.IsNullOrWhiteSpace(message.Name) ? messageRef : message.Name;
        }

        return messageRef;
    }
}
