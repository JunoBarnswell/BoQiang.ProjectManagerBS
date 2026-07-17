using System;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Listener;

public class DelegateExpressionTaskListener : ITaskListener
{
    public string? Event { get; set; }

    private readonly string? _delegateExpressionText;
    private readonly IExpressionManager? _expressionManager;

    public DelegateExpressionTaskListener() { }

    public DelegateExpressionTaskListener(
        string? @event = null,
        string? delegateExpressionText = null,
        IExpressionManager? expressionManager = null)
    {
        Event = @event;
        _delegateExpressionText = delegateExpressionText;
        _expressionManager = expressionManager;
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_delegateExpressionText)) return;

        var delegateInstance = ResolveDelegateExpression();
        if (delegateInstance != null)
        {
            await delegateInstance.ExecuteAsync(execution, cancellationToken);
        }
    }

    private IDelegateExpression? ResolveDelegateExpression()
    {
        if (string.IsNullOrEmpty(_delegateExpressionText)) return null;

        var expression = _delegateExpressionText.Trim();
        if (expression.StartsWith("${") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }
        else if (expression.StartsWith("#{") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }

        if (_expressionManager != null)
        {
            var result = _expressionManager.Evaluate(expression, new());
            if (result is IDelegateExpression delegateExpression) return delegateExpression;
        }

        var type = Type.GetType(expression);
        if (type != null)
        {
            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance is IDelegateExpression delegateExpression) return delegateExpression;
            }
            catch
            {
            }
        }

        return null;
    }
}

public class DelegateExpressionExecutionListener : IExecutionListener
{
    public string? Event { get; set; }

    private readonly string? _delegateExpressionText;
    private readonly IExpressionManager? _expressionManager;

    public DelegateExpressionExecutionListener() { }

    public DelegateExpressionExecutionListener(
        string? @event = null,
        string? delegateExpressionText = null,
        IExpressionManager? expressionManager = null)
    {
        Event = @event;
        _delegateExpressionText = delegateExpressionText;
        _expressionManager = expressionManager;
    }

    public async Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_delegateExpressionText)) return;

        var delegateInstance = ResolveDelegateExpression();
        if (delegateInstance != null)
        {
            await delegateInstance.ExecuteAsync(execution, cancellationToken);
        }
    }

    private IDelegateExpression? ResolveDelegateExpression()
    {
        if (string.IsNullOrEmpty(_delegateExpressionText)) return null;

        var expression = _delegateExpressionText.Trim();
        if (expression.StartsWith("${") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }
        else if (expression.StartsWith("#{") && expression.EndsWith("}"))
        {
            expression = expression[2..^1];
        }

        if (_expressionManager != null)
        {
            var result = _expressionManager.Evaluate(expression, new());
            if (result is IDelegateExpression delegateExpression) return delegateExpression;
        }

        var type = Type.GetType(expression);
        if (type != null)
        {
            try
            {
                var instance = Activator.CreateInstance(type);
                if (instance is IDelegateExpression delegateExpression) return delegateExpression;
            }
            catch
            {
            }
        }

        return null;
    }
}
