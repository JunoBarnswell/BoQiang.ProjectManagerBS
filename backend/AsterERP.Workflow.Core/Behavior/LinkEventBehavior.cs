using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Behavior;

public class IntermediateCatchLinkEventActivityBehavior : FlowNodeActivityBehavior, ITriggerableActivityBehavior
{
    public string? LinkName { get; set; }
    public string? LinkSource { get; set; }

    protected BpmnModelNs.LinkEventDefinition? LinkEventDefinition { get; set; }

    public IntermediateCatchLinkEventActivityBehavior() { }

    public IntermediateCatchLinkEventActivityBehavior(string? linkName, string? linkSource = null)
    {
        LinkName = linkName;
        LinkSource = linkSource;
    }

    public IntermediateCatchLinkEventActivityBehavior(
        BpmnModelNs.LinkEventDefinition linkEventDefinition)
    {
        LinkEventDefinition = linkEventDefinition;
        LinkName = linkEventDefinition.Name;
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_linkCatchEventWaiting", true);
        execution.SetVariableLocal("_linkName", LinkName);
        execution.SetVariable("_linkSubscription_" + LinkName, true);
        execution.IsActive = false;
    }

    public virtual async Task TriggerAsync(ExecutionEntity execution, string? signalName, object? signalData, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_linkCatchEventWaiting", false);
        execution.SetVariableLocal("_linkCatchTriggered", true);
        if (signalData != null)
        {
            execution.SetVariable("_linkCatchData", signalData);
        }
        await LeaveAsync(execution, cancellationToken);
    }

    public virtual async Task TriggerLinkAsync(ExecutionEntity execution, string? linkName, CancellationToken cancellationToken = default)
    {
        execution.SetVariableLocal("_linkCatchEventWaiting", false);
        execution.SetVariableLocal("_linkCatchTriggered", true);
        execution.SetVariableLocal("_triggeredLinkName", linkName);
        await LeaveAsync(execution, cancellationToken);
    }
}

public class IntermediateThrowLinkEventActivityBehavior : FlowNodeActivityBehavior
{
    public string? LinkName { get; set; }
    public string? LinkTarget { get; set; }

    protected BpmnModelNs.ThrowEvent? ThrowEvent { get; set; }

    public IntermediateThrowLinkEventActivityBehavior() { }

    public IntermediateThrowLinkEventActivityBehavior(string? linkName, string? linkTarget = null)
    {
        LinkName = linkName;
        LinkTarget = linkTarget;
    }

    public IntermediateThrowLinkEventActivityBehavior(BpmnModelNs.ThrowEvent throwEvent)
    {
        ThrowEvent = throwEvent;
        if (throwEvent.EventDefinitions != null)
        {
            foreach (var eventDef in throwEvent.EventDefinitions)
            {
                if (eventDef is BpmnModelNs.LinkEventDefinition linkDef)
                {
                    LinkName = linkDef.Name;
                    LinkTarget = linkDef.Target;
                    break;
                }
            }
        }
    }

    public override async Task ExecuteAsync(ExecutionEntity execution, CancellationToken cancellationToken = default)
    {
        execution.SetVariable("_thrownLinkName", LinkName);
        execution.SetVariable("_thrownLinkTarget", LinkTarget);
        execution.SetVariable("_thrownLinkTimestamp", AbpTimeIdProvider.UtcNow);
        execution.IsActive = false;
        await LeaveAsync(execution, cancellationToken);
    }
}

