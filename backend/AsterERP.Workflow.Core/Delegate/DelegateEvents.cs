using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Delegate;

public interface IWorkflowEntityEvent : IWorkflowEvent
{
    object Entity { get; }
}

public interface IWorkflowErrorEvent : IWorkflowEvent
{
    string? ErrorCode { get; }
    string? ErrorMessage { get; }
}

public interface IWorkflowEntityWithVariablesEvent : IWorkflowEntityEvent
{
    Dictionary<string, object?> Variables { get; }
    bool LocalScope { get; }
}

public interface IWorkflowEventDispatcher
{
    void AddEventListener(IWorkflowEventListener listener);
    void AddEventListener(IWorkflowEventListener listener, params WorkflowEventType[] types);
    void RemoveEventListener(IWorkflowEventListener listener);
    void DispatchEvent(IWorkflowEvent @event);
    Task DispatchEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default);
}
