using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Behavior;

public enum DelegateExecutionOutcome
{
    LeaveExecution,
    WaitForTrigger
}

public delegate DelegateExecutionOutcome DelegateExecutionFunction(ExecutionEntity execution);
