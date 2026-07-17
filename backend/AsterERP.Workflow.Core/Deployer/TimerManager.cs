using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.BpmnModel;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;
using AsterERP.Workflow.Core.Job;
using BpmnModelType = AsterERP.Workflow.BpmnModel.BpmnModel;

namespace AsterERP.Workflow.Core.Deployer;

public class TimerManager
{
    private readonly IExpressionManager? _expressionManager;
    private readonly IJobManager? _jobManager;

    public TimerManager() { }

    public TimerManager(
        IExpressionManager? expressionManager = null,
        IJobManager? jobManager = null)
    {
        _expressionManager = expressionManager;
        _jobManager = jobManager;
    }

    public async Task CreateTimersForProcessAsync(
        ProcessDefinitionInfo processDefinition,
        BpmnModelType bpmnModel,
        CancellationToken cancellationToken = default)
    {
        if (bpmnModel.Processes == null || bpmnModel.Processes.Count == 0) return;

        foreach (var process in bpmnModel.Processes)
        {
            await CreateTimersForProcessElementsAsync(processDefinition, process, cancellationToken);
        }
    }

    private async Task CreateTimersForProcessElementsAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.Process process,
        CancellationToken cancellationToken)
    {
        foreach (var flowElement in process.FlowElements)
        {
            await CreateTimersForFlowElementAsync(processDefinition, flowElement, cancellationToken);
        }
    }

    private async Task CreateTimersForFlowElementAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.FlowElement flowElement,
        CancellationToken cancellationToken)
    {
        switch (flowElement)
        {
            case AsterERP.Workflow.BpmnModel.StartEvent startEvent:
                await CreateStartEventTimersAsync(processDefinition, startEvent, cancellationToken);
                break;
            case AsterERP.Workflow.BpmnModel.IntermediateCatchEvent catchEvent:
                await CreateIntermediateCatchEventTimersAsync(processDefinition, catchEvent, cancellationToken);
                break;
            case AsterERP.Workflow.BpmnModel.BoundaryEvent boundaryEvent:
                await CreateBoundaryEventTimersAsync(processDefinition, boundaryEvent, cancellationToken);
                break;
            case AsterERP.Workflow.BpmnModel.SubProcess subProcess:
                foreach (var childElement in subProcess.FlowElements)
                {
                    await CreateTimersForFlowElementAsync(processDefinition, childElement, cancellationToken);
                }
                break;
        }
    }

    private async Task CreateStartEventTimersAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.StartEvent startEvent,
        CancellationToken cancellationToken)
    {
        if (startEvent.EventDefinitions == null) return;

        foreach (var eventDef in startEvent.EventDefinitions)
        {
            if (eventDef is AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef)
            {
                await ScheduleTimerForStartEventAsync(processDefinition, startEvent, timerDef, cancellationToken);
            }
        }
    }

    private async Task CreateIntermediateCatchEventTimersAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.IntermediateCatchEvent catchEvent,
        CancellationToken cancellationToken)
    {
        if (catchEvent.EventDefinitions == null) return;

        foreach (var eventDef in catchEvent.EventDefinitions)
        {
            if (eventDef is AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef)
            {
                await ScheduleTimerForIntermediateEventAsync(processDefinition, catchEvent, timerDef, cancellationToken);
            }
        }
    }

    private async Task CreateBoundaryEventTimersAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.BoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        if (boundaryEvent.EventDefinitions == null) return;

        foreach (var eventDef in boundaryEvent.EventDefinitions)
        {
            if (eventDef is AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef)
            {
                await ScheduleTimerForBoundaryEventAsync(processDefinition, boundaryEvent, timerDef, cancellationToken);
            }
        }
    }

    private async Task ScheduleTimerForStartEventAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.StartEvent startEvent,
        AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef,
        CancellationToken cancellationToken)
    {
        if (_jobManager == null) return;

        var dueDate = ResolveDueDate(timerDef);
        if (dueDate != null)
        {
            var timerJob = await _jobManager.CreateTimerJobAsync(
                processDefinition.Id,
                null,
                processDefinition.Id,
                dueDate,
                timerDef.TimeCycle,
                "timer-start-event",
                startEvent.Id,
                processDefinition.TenantId,
                cancellationToken);

            if (timerJob != null)
            {
                await _jobManager.ScheduleTimerJobAsync(timerJob, cancellationToken);
            }
        }
    }

    private async Task ScheduleTimerForIntermediateEventAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.IntermediateCatchEvent catchEvent,
        AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private async Task ScheduleTimerForBoundaryEventAsync(
        ProcessDefinitionInfo processDefinition,
        AsterERP.Workflow.BpmnModel.BoundaryEvent boundaryEvent,
        AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
    }

    private DateTime? ResolveDueDate(AsterERP.Workflow.BpmnModel.TimerEventDefinition timerDef)
    {
        if (!string.IsNullOrEmpty(timerDef.TimeDate))
        {
            if (DateTime.TryParse(timerDef.TimeDate, out var date)) return date;
        }

        if (!string.IsNullOrEmpty(timerDef.TimeDuration))
        {
            if (Behavior.TimerEventActivityBehavior.TryParseDuration(timerDef.TimeDuration, out var duration))
            {
                return AbpTimeIdProvider.UtcNow.Add(duration);
            }
        }

        return null;
    }

    public async Task CancelTimersByDeploymentAsync(
        string deploymentId,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }

    public async Task CancelTimersByProcessDefinitionAsync(
        string processDefinitionId,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
    }
}

