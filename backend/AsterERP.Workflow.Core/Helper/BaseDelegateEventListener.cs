using System;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public abstract class BaseDelegateEventListener : IWorkflowEventListener
{
    public Type? EntityType { get; set; }

    public abstract void OnEvent(IWorkflowEvent @event);

    public virtual bool IsFailOnException => false;

    protected virtual bool IsValidEvent(IWorkflowEvent @event)
    {
        if (EntityType == null)
            return true;

        return @event is IWorkflowEntityEvent entityEvent &&
               entityEvent.Entity != null &&
               EntityType.IsInstanceOfType(entityEvent.Entity);
    }
}
