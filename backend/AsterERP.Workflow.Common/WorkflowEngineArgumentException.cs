namespace AsterERP.Workflow.Common;

public class WorkflowEngineArgumentException : WorkflowEngineException
{
    public WorkflowEngineArgumentException(string message) : base(message) { }

    public WorkflowEngineArgumentException(string message, Exception innerException) : base(message, innerException) { }
}
