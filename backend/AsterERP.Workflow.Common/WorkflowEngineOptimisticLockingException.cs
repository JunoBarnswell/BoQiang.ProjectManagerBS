namespace AsterERP.Workflow.Common;

public class WorkflowEngineOptimisticLockingException : WorkflowEngineException
{
    public WorkflowEngineOptimisticLockingException(string message) : base(message) { }

    public WorkflowEngineOptimisticLockingException(string message, Exception innerException) : base(message, innerException) { }
}
