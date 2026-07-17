using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Core.Helper;

public interface IWorkflowEntityEvent : IWorkflowEvent
{
    object? Entity { get; }
}
