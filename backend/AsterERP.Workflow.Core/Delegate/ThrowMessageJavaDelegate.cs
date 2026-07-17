using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class ThrowMessageJavaDelegate : IThrowMessageDelegate
{
    private readonly Type _clazz;

    public ThrowMessageJavaDelegate(Type clazz)
    {
        _clazz = clazz;
    }

    public async Task<bool> SendAsync(IDelegateExecution execution, ThrowMessage message, CancellationToken cancellationToken = default)
    {
        var delegateObj = Activator.CreateInstance(_clazz);
        if (delegateObj is IThrowMessageDelegate throwMessageDelegate)
        {
            return await throwMessageDelegate.SendAsync(execution, message, cancellationToken);
        }
        return false;
    }
}

