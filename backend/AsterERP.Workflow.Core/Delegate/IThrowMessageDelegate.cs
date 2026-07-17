using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AsterERP.Workflow.Core.Delegate;

public interface IThrowMessageDelegate
{
    Task<bool> SendAsync(IDelegateExecution execution, ThrowMessage message, CancellationToken cancellationToken = default);
}

