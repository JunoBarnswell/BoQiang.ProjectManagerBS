using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Core.Delegate;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Event;
using AsterERP.Workflow.Core.Execution;
using AsterERP.Workflow.Core.Expression;

namespace AsterERP.Workflow.Core.Listener;

public interface ITransactionDependentTaskListener
{
    Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public interface ITransactionDependentExecutionListener
{
    Task NotifyAsync(IDelegateExecution execution, CancellationToken cancellationToken = default);
}

public class DelegateExpressionTransactionDependentTaskListener : ITransactionDependentTaskListener
{
    private readonly string? _delegateExpressionText;
    private readonly IExpressionManager? _expressionManager;

    public DelegateExpressionTransactionDependentTaskListener() { }

    public DelegateExpressionTransactionDependentTaskListener(
        string? delegateExpressionText = null,
        IExpressionManager? expressionManager = null)
    {
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
            expression = expression[2..^1];
        else if (expression.StartsWith("#{") && expression.EndsWith("}"))
            expression = expression[2..^1];

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

public class DelegateExpressionTransactionDependentExecutionListener : ITransactionDependentExecutionListener
{
    private readonly string? _delegateExpressionText;
    private readonly IExpressionManager? _expressionManager;

    public DelegateExpressionTransactionDependentExecutionListener() { }

    public DelegateExpressionTransactionDependentExecutionListener(
        string? delegateExpressionText = null,
        IExpressionManager? expressionManager = null)
    {
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
            expression = expression[2..^1];
        else if (expression.StartsWith("#{") && expression.EndsWith("}"))
            expression = expression[2..^1];

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
