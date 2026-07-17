using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.EventLogger.EventHandler;

public interface IEventHandler
{
    EventLogEntry? HandleEvent(IWorkflowEvent @event);
}
