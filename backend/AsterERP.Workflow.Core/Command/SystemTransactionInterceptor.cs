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

public class SystemTransactionInterceptor : TransactionInterceptor
{
    public SystemTransactionInterceptor(IProcessEngineConfiguration processEngineConfiguration)
        : base(processEngineConfiguration)
    {
    }

    protected override T ExecuteRequired<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        if (Transaction.Current != null)
        {
            return next(command);
        }

        using var scope = new TransactionScope(TransactionScopeOption.Required);
        var result = next(command);
        scope.Complete();
        return result;
    }

    protected override T ExecuteRequiresNew<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        using var scope = new TransactionScope(TransactionScopeOption.RequiresNew);
        var result = next(command);
        scope.Complete();
        return result;
    }

    protected override async Task<T> ExecuteRequiredAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken)
    {
        if (Transaction.Current != null)
        {
            return await next(command);
        }

        using var scope = new TransactionScope(TransactionScopeOption.Required, TransactionScopeAsyncFlowOption.Enabled);
        var result = await next(command);
        scope.Complete();
        return result;
    }

    protected override async Task<T> ExecuteRequiresNewAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken)
    {
        using var scope = new TransactionScope(TransactionScopeOption.RequiresNew, TransactionScopeAsyncFlowOption.Enabled);
        var result = await next(command);
        scope.Complete();
        return result;
    }
}
