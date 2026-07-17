using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Execution;
using BpmnModelNs = AsterERP.Workflow.BpmnModel;

namespace AsterERP.Workflow.Core.Agenda;

public abstract class AbstractOperation
{
    protected IAgenda Agenda { get; }
    protected ExecutionEntity? Execution { get; }
    protected IProcessEngineConfiguration EngineConfig { get; }

    protected AbstractOperation(IAgenda agenda, ExecutionEntity? execution, IProcessEngineConfiguration engineConfig)
    {
        Agenda = agenda;
        Execution = execution;
        EngineConfig = engineConfig;
    }

    public abstract Task RunAsync(CancellationToken cancellationToken = default);

    protected BpmnModelNs.FlowElement? GetCurrentFlowElement(ExecutionEntity execution)
    {
        if (execution.CurrentFlowElement != null)
            return execution.CurrentFlowElement;

        if (execution.CurrentActivityId != null && execution.Process != null)
        {
            return execution.Process.FlowElements.Find(e => e.Id == execution.CurrentActivityId) as BpmnModelNs.FlowNode;
        }

        return null;
    }

    protected void ExecuteExecutionListeners(BpmnModelNs.FlowElement flowElement, string eventType)
    {
        if (flowElement.ExecutionListeners == null || flowElement.ExecutionListeners.Count == 0)
            return;

        if (Execution == null) return;

        Execution.EventName = eventType;

        foreach (var listener in flowElement.ExecutionListeners)
        {
            if (listener.Event == eventType)
            {
                ExecuteSingleListener(listener);
            }
        }

        Execution.EventName = null;
    }

    private void ExecuteSingleListener(BpmnModelNs.WorkflowExtensionListener listener)
    {
        if (listener.ImplementationType == "class" && !string.IsNullOrEmpty(listener.Implementation))
        {
            var type = Type.GetType(listener.Implementation);
            if (type != null)
            {
                var instance = Activator.CreateInstance(type) as Event.IWorkflowEventListener;
                instance?.OnEvent(new Event.WorkflowEventImplementation(
                    Event.WorkflowEventType.CUSTOM,
                    Execution?.Id,
                    Execution?.ProcessInstanceId,
                    Execution?.ProcessDefinitionId));
            }
        }
    }

    protected ExecutionEntity? FindFirstParentScopeExecution(ExecutionEntity executionEntity)
    {
        var current = executionEntity.Parent;
        while (current != null)
        {
            if (current.IsScope)
                return current;
            current = current.Parent;
        }
        return null;
    }
}
