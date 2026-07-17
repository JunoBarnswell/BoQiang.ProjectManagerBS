namespace AsterERP.Workflow.Common;

public class WorkflowEngineWrongDbException : WorkflowEngineException
{
    public WorkflowEngineWrongDbException(Type expected, Type actual)
        : base($"Expected database schema version {expected.Name} but found {actual.Name}") { }
}
