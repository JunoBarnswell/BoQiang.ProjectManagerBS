using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Delegate;

public interface IDelegateExecution : IExecution, IVariableScope
{
    new string? CurrentActivityId { get; }
    new string? EventName { get; }
}
