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

public abstract class DelegateInvocation
{
    public object? Result { get; protected set; }

    public abstract void Proceed();
    public virtual Task ProceedAsync()
    {
        Proceed();
        return Task.CompletedTask;
    }
}

