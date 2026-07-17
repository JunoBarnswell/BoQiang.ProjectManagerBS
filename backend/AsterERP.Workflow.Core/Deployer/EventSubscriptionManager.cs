using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.BpmnModel;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class EventSubscriptionManager
{
    private readonly List<EventSubscriptionInfo> _subscriptions = new();

    public EventSubscriptionManager() { }

    public IReadOnlyCollection<EventSubscriptionInfo> Subscriptions => _subscriptions.ToList();

    public IReadOnlyCollection<EventSubscriptionInfo> FindSubscriptions(
        string? eventType = null,
        string? eventName = null,
        string? processDefinitionId = null,
        string? activityId = null,
        string? tenantId = null)
    {
        return _subscriptions
            .Where(subscription =>
                (eventType == null || subscription.EventType == eventType) &&
                (eventName == null || subscription.EventName == eventName) &&
                (processDefinitionId == null || subscription.ProcessDefinitionId == processDefinitionId) &&
                (activityId == null || subscription.ActivityId == activityId) &&
                (tenantId == null || subscription.TenantId == tenantId))
            .ToList();
    }

    public async Task CreateEventSubscriptionsAsync(
        ProcessDefinitionInfo processDefinition,
        BpmnModelType bpmnModel,
        CancellationToken cancellationToken = default)
    {
        if (bpmnModel.Processes == null || bpmnModel.Processes.Count == 0) return;

        foreach (var process in bpmnModel.Processes)
        {
            await CreateEventSubscriptionsForProcessAsync(processDefinition, process, bpmnModel, cancellationToken);
        }
    }

    private async Task CreateEventSubscriptionsForProcessAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.Process process,
        BpmnModelType bpmnModel,
        CancellationToken cancellationToken)
    {
        foreach (var flowElement in process.FlowElements)
        {
            await CreateEventSubscriptionsForFlowElementAsync(processDefinition, flowElement, bpmnModel, cancellationToken);
        }
    }

    private async Task CreateEventSubscriptionsForFlowElementAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.FlowElement flowElement,
        BpmnModelType bpmnModel,
        CancellationToken cancellationToken)
    {
        switch (flowElement)
        {
            case AsterERP.Workflow.BpmnModel.StartEvent startEvent:
                await CreateStartEventSubscriptionsAsync(processDefinition, startEvent, bpmnModel, cancellationToken);
                break;
            case AsterERP.Workflow.BpmnModel.IntermediateCatchEvent intermediateCatchEvent:
                await CreateIntermediateCatchEventSubscriptionsAsync(processDefinition, intermediateCatchEvent, bpmnModel, cancellationToken);
                break;
            case AsterERP.Workflow.BpmnModel.BoundaryEvent boundaryEvent:
                await CreateBoundaryEventSubscriptionsAsync(processDefinition, boundaryEvent, bpmnModel, cancellationToken);
                break;
            case AsterERP.Workflow.BpmnModel.SubProcess subProcess when subProcess.TriggeredByEvent:
                await CreateEventSubProcessSubscriptionsAsync(processDefinition, subProcess, bpmnModel, cancellationToken);
                break;
        }
    }

    private async Task CreateStartEventSubscriptionsAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.StartEvent startEvent,
        BpmnModelType? bpmnModel,
        CancellationToken cancellationToken)
    {
        if (startEvent.EventDefinitions == null) return;

        foreach (var eventDef in startEvent.EventDefinitions)
        {
            if (eventDef is AsterERP.Workflow.BpmnModel.SignalEventDefinition signalDef)
            {
                CreateSignalEventSubscription(processDefinition, ResolveSignalName(bpmnModel, signalDef), startEvent.Id!);
            }
            else if (eventDef is AsterERP.Workflow.BpmnModel.MessageEventDefinition messageDef)
            {
                CreateMessageEventSubscription(processDefinition, ResolveMessageName(bpmnModel, messageDef), startEvent.Id!, isStartEvent: true);
            }
            else if (eventDef is AsterERP.Workflow.BpmnModel.EscalationEventDefinition escalationDef)
            {
                CreateEscalationEventSubscription(processDefinition, ResolveEscalationName(escalationDef), startEvent.Id!);
            }
        }

        await Task.CompletedTask;
    }

    private async Task CreateIntermediateCatchEventSubscriptionsAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.IntermediateCatchEvent catchEvent,
        BpmnModelType? bpmnModel,
        CancellationToken cancellationToken)
    {
        if (catchEvent.EventDefinitions == null) return;

        foreach (var eventDef in catchEvent.EventDefinitions)
        {
            if (eventDef is AsterERP.Workflow.BpmnModel.SignalEventDefinition signalDef)
            {
                CreateSignalEventSubscription(processDefinition, ResolveSignalName(bpmnModel, signalDef), catchEvent.Id!);
            }
            else if (eventDef is AsterERP.Workflow.BpmnModel.MessageEventDefinition messageDef)
            {
                CreateMessageEventSubscription(processDefinition, ResolveMessageName(bpmnModel, messageDef), catchEvent.Id!, isStartEvent: false);
            }
            else if (eventDef is AsterERP.Workflow.BpmnModel.EscalationEventDefinition escalationDef)
            {
                CreateEscalationEventSubscription(processDefinition, ResolveEscalationName(escalationDef), catchEvent.Id!);
            }
        }

        await Task.CompletedTask;
    }

    private async Task CreateBoundaryEventSubscriptionsAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.BoundaryEvent boundaryEvent,
        BpmnModelType? bpmnModel,
        CancellationToken cancellationToken)
    {
        if (boundaryEvent.EventDefinitions == null) return;

        foreach (var eventDef in boundaryEvent.EventDefinitions)
        {
            if (eventDef is AsterERP.Workflow.BpmnModel.SignalEventDefinition signalDef)
            {
                CreateSignalEventSubscription(processDefinition, ResolveSignalName(bpmnModel, signalDef), boundaryEvent.Id!);
            }
            else if (eventDef is AsterERP.Workflow.BpmnModel.MessageEventDefinition messageDef)
            {
                CreateMessageEventSubscription(processDefinition, ResolveMessageName(bpmnModel, messageDef), boundaryEvent.Id!, isStartEvent: false);
            }
            else if (eventDef is AsterERP.Workflow.BpmnModel.EscalationEventDefinition escalationDef)
            {
                CreateEscalationEventSubscription(processDefinition, ResolveEscalationName(escalationDef), boundaryEvent.Id!);
            }
        }

        await Task.CompletedTask;
    }

    private async Task CreateEventSubProcessSubscriptionsAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.SubProcess subProcess,
        BpmnModelType? bpmnModel,
        CancellationToken cancellationToken)
    {
        foreach (var childElement in subProcess.FlowElements)
        {
            if (childElement is AsterERP.Workflow.BpmnModel.StartEvent startEvent && startEvent.EventDefinitions != null)
            {
                foreach (var eventDef in startEvent.EventDefinitions)
                {
                    if (eventDef is AsterERP.Workflow.BpmnModel.SignalEventDefinition signalDef)
                    {
                        CreateSignalEventSubscription(processDefinition, ResolveSignalName(bpmnModel, signalDef), startEvent.Id!);
                    }
                    else if (eventDef is AsterERP.Workflow.BpmnModel.MessageEventDefinition messageDef)
                    {
                        CreateMessageEventSubscription(processDefinition, ResolveMessageName(bpmnModel, messageDef), startEvent.Id!, isStartEvent: false);
                    }
                    else if (eventDef is AsterERP.Workflow.BpmnModel.ErrorEventDefinition errorDef)
                    {
                        CreateErrorEventSubscription(processDefinition, errorDef.ErrorCode, startEvent.Id!);
                    }
                    else if (eventDef is AsterERP.Workflow.BpmnModel.EscalationEventDefinition escalationDef)
                    {
                        CreateEscalationEventSubscription(
                            processDefinition,
                            ResolveEscalationName(escalationDef),
                            startEvent.Id!);
                    }
                }
            }
        }

        await Task.CompletedTask;
    }

    private void CreateSignalEventSubscription(ProcessDefinitionInfo processDefinition, string? signalRef, string activityId)
    {
        if (string.IsNullOrWhiteSpace(signalRef))
            throw new InvalidOperationException(
                $"Process definition '{processDefinition.Id}' activity '{activityId}' has signal event definition without resolvable signalRef.");

        AddSubscription(processDefinition, "signal", signalRef, activityId, configuration: null);
    }

    private void CreateMessageEventSubscription(ProcessDefinitionInfo processDefinition, string? messageRef, string activityId, bool isStartEvent)
    {
        if (string.IsNullOrWhiteSpace(messageRef))
            throw new InvalidOperationException(
                $"Process definition '{processDefinition.Id}' activity '{activityId}' has message event definition without resolvable messageRef.");

        var existing = isStartEvent
            ? _subscriptions.FirstOrDefault(subscription =>
            subscription.EventType == "message" &&
            subscription.EventName == messageRef &&
            subscription.Configuration != null &&
            subscription.ProcessDefinitionId != processDefinition.Id &&
            subscription.TenantId == processDefinition.TenantId)
            : null;

        if (existing != null)
            throw new InvalidOperationException(
                $"Cannot deploy process definition '{processDefinition.Id}' because message start event '{messageRef}' is already subscribed by process definition '{existing.ProcessDefinitionId}'");

        AddSubscription(processDefinition, "message", messageRef, activityId, configuration: isStartEvent ? processDefinition.Id : null);
    }

    private void CreateErrorEventSubscription(ProcessDefinitionInfo processDefinition, string? errorCode, string activityId)
    {
        if (string.IsNullOrEmpty(errorCode)) return;
        AddSubscription(processDefinition, "error", errorCode, activityId, configuration: null);
    }

    private void CreateEscalationEventSubscription(ProcessDefinitionInfo processDefinition, string? escalationName, string activityId)
    {
        if (string.IsNullOrWhiteSpace(escalationName))
            throw new InvalidOperationException(
                $"Process definition '{processDefinition.Id}' activity '{activityId}' has escalation event definition without resolvable escalation code/ref.");

        AddSubscription(processDefinition, "escalation", escalationName, activityId, configuration: null);
    }

    public async Task DeleteEventSubscriptionsByDeploymentAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        _subscriptions.RemoveAll(subscription => subscription.DeploymentId == deploymentId);
        await Task.CompletedTask;
    }

    public async Task DeleteEventSubscriptionsByProcessDefinitionAsync(
        string processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        _subscriptions.RemoveAll(subscription => subscription.ProcessDefinitionId == processDefinitionId);
        await Task.CompletedTask;
    }

    private void AddSubscription(
        ProcessDefinitionInfo processDefinition,
        string eventType,
        string eventName,
        string activityId,
        string? configuration)
    {
        if (_subscriptions.Any(subscription =>
            subscription.EventType == eventType &&
            subscription.EventName == eventName &&
            subscription.ProcessDefinitionId == processDefinition.Id &&
            subscription.ActivityId == activityId &&
            subscription.TenantId == processDefinition.TenantId))
        {
            return;
        }

        _subscriptions.Add(new EventSubscriptionInfo
        {
            EventType = eventType,
            EventName = eventName,
            ActivityId = activityId,
            ProcessDefinitionId = processDefinition.Id,
            ProcessDefinitionKey = processDefinition.Key,
            DeploymentId = processDefinition.DeploymentId,
            TenantId = processDefinition.TenantId,
            Configuration = configuration
        });
    }

    private static string? ResolveMessageName(BpmnModelType? bpmnModel, AsterERP.Workflow.BpmnModel.MessageEventDefinition messageDef)
    {
        if (string.IsNullOrWhiteSpace(messageDef.MessageRef))
            return null;

        if (bpmnModel == null)
            throw new InvalidOperationException(
                $"Message event definition references '{messageDef.MessageRef}', but BPMN model is not available for message resolution.");

        if (!bpmnModel.MessageMap.TryGetValue(messageDef.MessageRef, out var message))
            throw new InvalidOperationException(
                $"Message event definition references missing messageRef '{messageDef.MessageRef}'.");

        if (string.IsNullOrWhiteSpace(message.Name))
            throw new InvalidOperationException(
                $"Message '{messageDef.MessageRef}' must declare non-empty name for message event subscription.");

        return message.Name;
    }

    private static string? ResolveSignalName(BpmnModelType? bpmnModel, AsterERP.Workflow.BpmnModel.SignalEventDefinition signalDef)
    {
        if (string.IsNullOrWhiteSpace(signalDef.SignalRef))
            return null;

        if (bpmnModel == null)
            throw new InvalidOperationException(
                $"Signal event definition references '{signalDef.SignalRef}', but BPMN model is not available for signal resolution.");

        var signal = bpmnModel.Signals.FirstOrDefault(candidate => candidate.Id == signalDef.SignalRef);
        if (signal == null)
            throw new InvalidOperationException(
                $"Signal event definition references missing signalRef '{signalDef.SignalRef}'.");

        if (string.IsNullOrWhiteSpace(signal.Name))
            throw new InvalidOperationException(
                $"Signal '{signalDef.SignalRef}' must declare non-empty name for signal event subscription.");

        return signal.Name;
    }

    private static string? ResolveEscalationName(AsterERP.Workflow.BpmnModel.EscalationEventDefinition escalationDef)
    {
        if (!string.IsNullOrWhiteSpace(escalationDef.EscalationCode))
            return escalationDef.EscalationCode;

        if (!string.IsNullOrWhiteSpace(escalationDef.EscalationRef))
            return escalationDef.EscalationRef;

        return null;
    }
}
