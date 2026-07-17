using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;
using AsterERP.Workflow.Core.Agenda;
using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Context;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Services;
using System.Linq;

namespace AsterERP.Workflow.Core.Cmd;

public class SignalEventReceivedCmd : ICommand<object?>
{
    private readonly string _signalName;
    private readonly string? _executionId;
    private readonly Dictionary<string, object?>? _payload;
    private readonly bool _async;
    private readonly string? _tenantId;

    public SignalEventReceivedCmd(
        string signalName,
        string? executionId = null,
        Dictionary<string, object?>? processVariables = null,
        string? tenantId = null)
    {
        _signalName = signalName ?? throw new ArgumentNullException(nameof(signalName));
        _executionId = executionId;
        _payload = processVariables != null ? new Dictionary<string, object?>(processVariables) : null;
        _async = false;
        _tenantId = tenantId;
    }

    public SignalEventReceivedCmd(
        string signalName,
        string? executionId,
        bool isAsync,
        string? tenantId = null)
    {
        _signalName = signalName ?? throw new ArgumentNullException(nameof(signalName));
        _executionId = executionId;
        _async = isAsync;
        _payload = null;
        _tenantId = tenantId;
    }


    public async Task<object?> ExecuteAsync(ICommandContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_signalName))
            throw new WorkflowEngineArgumentException("signalName is null");

        var triggeredExecutions = new List<ExecutionEntity>();

        if (_executionId != null)
        {
            var execution = await context.GetCurrentExecutionAsync(_executionId, cancellationToken);
            if (execution == null)
            {
                throw new WorkflowEngineObjectNotFoundException(
                    $"Execution '{_executionId}' could not be found",
                    typeof(ExecutionEntity));
            }
            TriggerSignalSubscription(execution, requireSubscription: true, triggeredExecutions);
        }
        else
        {
            var matchingExecutions = await ResolveSignalSubscriptionExecutionsAsync(context, cancellationToken);

            foreach (var execution in matchingExecutions)
            {
                TriggerSignalSubscription(execution, requireSubscription: false, triggeredExecutions);
            }
        }

        var eventDispatcher = context.ProcessEngineConfiguration.EventDispatcher;
        if (eventDispatcher.IsEnabled)
        {
            await eventDispatcher.DispatchEventAsync(
                WorkflowEventBuilder.CreateCustomEvent("SIGNAL_EVENT_RECEIVED",
                    new Dictionary<string, object?>
                    {
                        ["signalName"] = _signalName,
                        ["executionId"] = _executionId,
                        ["async"] = _async
                    }), cancellationToken);
        }

        if (triggeredExecutions.Count > 0)
        {
            var agenda = new WorkflowEngineAgendaFactory(context.ProcessEngineConfiguration).CreateAgenda();
            foreach (var triggeredExecution in triggeredExecutions)
            {
                agenda.PlanTriggerExecutionOperation(triggeredExecution);
            }

            while (!agenda.IsEmpty)
            {
                await agenda.ExecuteNextAsync(cancellationToken);
            }
        }

        return null;
    }

    private bool HasSignalSubscription(ExecutionEntity execution)
    {
        var subscriptionName = execution.GetVariableLocal("_signalSubscriptionName") as string;
        return subscriptionName == _signalName
               || Equals(execution.GetVariable("_signalSubscription_" + _signalName), true)
               || HasSignalCatchSubscription(execution);
    }

    private bool HasSignalCatchSubscription(ExecutionEntity execution)
    {
        if (execution.CurrentFlowElement is not AsterERP.Workflow.BpmnModel.CatchEvent catchEvent)
        {
            return false;
        }

        foreach (var eventDefinition in catchEvent.EventDefinitions.OfType<AsterERP.Workflow.BpmnModel.SignalEventDefinition>())
        {
            var signalName = ResolveSignalName(execution, eventDefinition);
            if (string.Equals(signalName, _signalName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? ResolveSignalName(
        ExecutionEntity execution,
        AsterERP.Workflow.BpmnModel.SignalEventDefinition eventDefinition)
    {
        if (string.IsNullOrWhiteSpace(eventDefinition.SignalRef))
        {
            return null;
        }

        var signalRef = eventDefinition.SignalRef!;
        var signal = execution.Process?.BpmnModel?.Signals.FirstOrDefault(item =>
            string.Equals(item.Id, signalRef, StringComparison.Ordinal));
        if (signal != null)
        {
            return string.IsNullOrWhiteSpace(signal.Name) ? signalRef : signal.Name;
        }

        var processSignal = execution.Process?.Signals.FirstOrDefault(item =>
            string.Equals(item.Id, signalRef, StringComparison.Ordinal));
        if (processSignal != null)
        {
            return string.IsNullOrWhiteSpace(processSignal.Name) ? signalRef : processSignal.Name;
        }

        return signalRef;
    }

    private async Task<IReadOnlyCollection<ExecutionEntity>> ResolveSignalSubscriptionExecutionsAsync(
        ICommandContext context,
        CancellationToken cancellationToken)
    {
        var candidateExecutions = await context.FindExecutionsAsync(cancellationToken: cancellationToken);
        var candidateExecutionIds = candidateExecutions
            .Where(HasSignalSubscription)
            .Select(execution => execution.Id)
            .ToHashSet(StringComparer.Ordinal);

        var store = ProcessEngineServiceProviderAccessor.GetService<IWorkflowPersistenceStore>(context.ProcessEngineConfiguration);
        if (store?.IsEnabled == true)
        {
            var subscribedExecutionIds = await store.FindExecutionIdsBySignalSubscriptionAsync(
                _signalName,
                _tenantId,
                cancellationToken);
            foreach (var executionId in subscribedExecutionIds
                         .Where(executionId => !string.IsNullOrWhiteSpace(executionId)))
            {
                candidateExecutionIds.Add(executionId!);
            }
        }

        var executions = new List<ExecutionEntity>(candidateExecutionIds.Count);
        foreach (var candidateExecutionId in candidateExecutionIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var execution = await context.GetCurrentExecutionAsync(candidateExecutionId, cancellationToken);
            if (execution != null && HasSignalSubscription(execution))
            {
                executions.Add(execution);
            }
        }

        return executions;
    }

    private void TriggerSignalSubscription(ExecutionEntity execution, bool requireSubscription, List<ExecutionEntity> triggeredExecutions)
    {
        if (execution.IsEnded)
            throw new WorkflowEngineException(
                $"Cannot throw signal event '{_signalName}' because execution '{execution.Id}' is ended");

        var hasSubscription = HasSignalSubscription(execution);
        if (requireSubscription && !hasSubscription)
        {
            throw new WorkflowEngineObjectNotFoundException(
                $"Execution '{execution.Id}' does not have a signal subscription for '{_signalName}'");
        }

        if (!hasSubscription)
            return;

        if (_payload != null)
        {
            foreach (var kvp in _payload)
                execution.SetVariable(kvp.Key, kvp.Value);
        }

        execution.RemoveVariable("_signalSubscription_" + _signalName);
        execution.SetVariableLocal("_signalSubscriptionActive", false);
        execution.SetVariableLocal("_signalReceived_" + _signalName, true);
        execution.SetVariableLocal("_signalName", _signalName);
        execution.IsActive = true;
        triggeredExecutions.Add(execution);
    }
}
