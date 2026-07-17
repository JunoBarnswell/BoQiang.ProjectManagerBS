using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public class ThrowMessageDelegate : IThrowMessageDelegate
{
    private readonly Func<IDelegateExecution, ThrowMessage, CancellationToken, Task<bool>> _sendAsync;

    public ThrowMessageDelegate()
    {
        _sendAsync = (_, _, _) => Task.FromResult(true);
    }

    public ThrowMessageDelegate(Func<IDelegateExecution, ThrowMessage, CancellationToken, Task<bool>> sendAsync)
    {
        _sendAsync = sendAsync ?? throw new ArgumentNullException(nameof(sendAsync));
    }

    public Task<bool> SendAsync(IDelegateExecution execution, ThrowMessage message, CancellationToken cancellationToken = default)
    {
        return _sendAsync(execution, message, cancellationToken);
    }
}

