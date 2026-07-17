using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;

namespace AsterERP.Workflow.Core.Impl;

public interface ICondition
{
    Task<bool> EvaluateAsync(string sequenceFlowId, IDelegateExecution execution, CancellationToken cancellationToken = default);
}
