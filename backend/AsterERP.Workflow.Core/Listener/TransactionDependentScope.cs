using System.Collections.Generic;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Listener;

public class TransactionDependentTaskListenerExecutionScope
{
    public ITransactionDependentTaskListener Listener { get; }
    public ExecutionEntity Execution { get; }
    public Dictionary<string, object?> CurrentVariables { get; }
    public Dictionary<string, object?> CurrentEventVariables { get; }

    public TransactionDependentTaskListenerExecutionScope(
        ITransactionDependentTaskListener listener,
        ExecutionEntity execution,
        Dictionary<string, object?> currentVariables,
        Dictionary<string, object?> currentEventVariables)
    {
        Listener = listener;
        Execution = execution;
        CurrentVariables = currentVariables;
        CurrentEventVariables = currentEventVariables;
    }

    public DelegateExecution CreateDelegateExecution()
    {
        var delegateExec = new DelegateExecution(Execution);
        foreach (var kv in CurrentVariables)
        {
            delegateExec.SetVariable(kv.Key, kv.Value);
        }
        foreach (var kv in CurrentEventVariables)
        {
            delegateExec.SetVariable(kv.Key, kv.Value);
        }
        return delegateExec;
    }
}

public class TransactionDependentExecutionListenerExecutionScope
{
    public ITransactionDependentExecutionListener Listener { get; }
    public ExecutionEntity Execution { get; }
    public Dictionary<string, object?> CurrentVariables { get; }
    public Dictionary<string, object?> CurrentEventVariables { get; }

    public TransactionDependentExecutionListenerExecutionScope(
        ITransactionDependentExecutionListener listener,
        ExecutionEntity execution,
        Dictionary<string, object?> currentVariables,
        Dictionary<string, object?> currentEventVariables)
    {
        Listener = listener;
        Execution = execution;
        CurrentVariables = currentVariables;
        CurrentEventVariables = currentEventVariables;
    }

    public DelegateExecution CreateDelegateExecution()
    {
        var delegateExec = new DelegateExecution(Execution);
        foreach (var kv in CurrentVariables)
        {
            delegateExec.SetVariable(kv.Key, kv.Value);
        }
        foreach (var kv in CurrentEventVariables)
        {
            delegateExec.SetVariable(kv.Key, kv.Value);
        }
        return delegateExec;
    }
}
