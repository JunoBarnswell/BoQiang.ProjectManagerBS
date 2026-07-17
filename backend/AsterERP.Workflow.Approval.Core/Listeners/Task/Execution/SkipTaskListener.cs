using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Event;

namespace AsterERP.Workflow.Approval.Core.Listeners.Task.Execution;

public class SkipTaskListener : IWorkflowEventListener
{
    public bool IsFailOnException => true;

    public void OnEvent(IWorkflowEvent @event)
    {
        if (@event is IWorkflowEntityEvent entityEvent && entityEvent.Entity is IDictionary<string, object> flowElement)
        {
            flowElement["skipExpression"] = "${1==1}";
        }
    }
}
