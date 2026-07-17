namespace AsterERP.Workflow.Common;

public class WorkflowTaskAlreadyClaimedException : WorkflowEngineException
{
    public WorkflowTaskAlreadyClaimedException(string message) : base(message) { }
}
