using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;

namespace AsterERP.Workflow.Core.Listener;

public class ExecuteTaskListenerTransactionListener : IWorkflowEventListener
{
    private readonly ITaskListener _taskListener;
    private readonly ExecutionEntity _execution;

    public bool IsFailOnException => true;

    public ExecuteTaskListenerTransactionListener(
        ITaskListener taskListener,
        ExecutionEntity execution)
    {
        _taskListener = taskListener;
        _execution = execution;
    }

    public void OnEvent(IWorkflowEvent @event)
    {
        throw new NotSupportedException("ExecuteTaskListenerTransactionListener is async-only. Use OnEventAsync.");
    }

    public async Task OnEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default)
    {
        var delegateExecution = new DelegateExecution(_execution);
        await _taskListener.NotifyAsync(delegateExecution, cancellationToken);
    }
}

public class ExecuteExecutionListenerTransactionListener : IWorkflowEventListener
{
    private readonly IExecutionListener _executionListener;
    private readonly ExecutionEntity _execution;

    public bool IsFailOnException => true;

    public ExecuteExecutionListenerTransactionListener(
        IExecutionListener executionListener,
        ExecutionEntity execution)
    {
        _executionListener = executionListener;
        _execution = execution;
    }

    public void OnEvent(IWorkflowEvent @event)
    {
        throw new NotSupportedException("ExecuteExecutionListenerTransactionListener is async-only. Use OnEventAsync.");
    }

    public async Task OnEventAsync(IWorkflowEvent @event, CancellationToken cancellationToken = default)
    {
        var delegateExecution = new DelegateExecution(_execution);
        await _executionListener.NotifyAsync(delegateExecution, cancellationToken);
    }
}
