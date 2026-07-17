using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Context;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AsterERP.Workflow.Core.Command;

public class DefaultDelegateInterceptor : IDelegateInterceptor
{
    public void HandleInvocation(DelegateInvocation invocation)
    {
        invocation.Proceed();
    }

    public async Task HandleInvocationAsync(DelegateInvocation invocation)
    {
        await invocation.ProceedAsync();
    }
}

