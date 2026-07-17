namespace AsterERP.Workflow.Common;

public class WorkflowEngineObjectNotFoundException : WorkflowEngineException
{
    public string? ObjectName { get; }

    public Type? ObjectType { get; }

    public WorkflowEngineObjectNotFoundException(string message) : base(message) { }

    public WorkflowEngineObjectNotFoundException(string message, Type? objectType) : base(message)
    {
        ObjectType = objectType;
    }

    public WorkflowEngineObjectNotFoundException(string message, Exception innerException) : base(message, innerException) { }
}
