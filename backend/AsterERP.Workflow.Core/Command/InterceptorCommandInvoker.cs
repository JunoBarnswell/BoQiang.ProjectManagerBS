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

public class InterceptorCommandInvoker : AbstractCommandInterceptor
{
    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        return command.Execute(CommandContextStack.Current!);
    }

    public override Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        return command.ExecuteAsync(CommandContextStack.Current!, cancellationToken);
    }
}

