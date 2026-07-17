using AsterERP.Workflow.Core.Delegate;

namespace AsterERP.Workflow.Core.Delegate;

public interface IWorkflowDelegate
{
    global::System.Threading.Tasks.Task ExecuteAsync(IDelegateExecution execution);
}
