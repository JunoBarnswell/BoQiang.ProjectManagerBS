using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class ThrowMessageDelegateExpression : IThrowMessageDelegate
{
    private readonly IExpression _delegateExpression;

    public ThrowMessageDelegateExpression(IExpression delegateExpression)
    {
        _delegateExpression = delegateExpression;
    }

    public async Task<bool> SendAsync(IDelegateExecution execution, ThrowMessage message, CancellationToken cancellationToken = default)
    {
        var delegateObj = _delegateExpression.GetValue(execution);
        if (delegateObj is IThrowMessageDelegate throwMessageDelegate)
        {
            return await throwMessageDelegate.SendAsync(execution, message, cancellationToken);
        }
        return false;
    }
}

