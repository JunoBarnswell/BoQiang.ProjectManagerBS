using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Listener;

public class ExpressionTaskListener : ITaskListener
{
    public string? Event { get; set; }

    private readonly IWorkflowExpression? _expression;

    public ExpressionTaskListener() { }

    public ExpressionTaskListener(
        string? @event = null,
        IWorkflowExpression? expression = null)
    {
        Event = @event;
        _expression = expression;
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        if (_expression == null) return;

        var result = _expression.GetValue(execution);
        if (result != null)
        {
            execution.SetVariable("_expressionTaskListenerResult", result);
        }

        await Task.CompletedTask;
    }
}

public class ExpressionExecutionListener : IExecutionListener
{
    public string? Event { get; set; }

    private readonly IWorkflowExpression? _expression;

    public ExpressionExecutionListener() { }

    public ExpressionExecutionListener(
        string? @event = null,
        IWorkflowExpression? expression = null)
    {
        Event = @event;
        _expression = expression;
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        if (_expression == null) return;

        var result = _expression.GetValue(execution);
        if (result != null)
        {
            execution.SetVariable("_expressionExecutionListenerResult", result);
        }

        await Task.CompletedTask;
    }
}
