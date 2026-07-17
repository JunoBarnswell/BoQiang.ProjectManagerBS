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

public class TransactionContextInterceptor : AbstractCommandInterceptor
{
    public TransactionContextInterceptor()
    {
    }

    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        var propagation = CommandConfigStack.Current?.TransactionPropagation ?? TransactionPropagation.Required;

        if (propagation == TransactionPropagation.NotSupported)
        {
            return next(command);
        }

        if (propagation == TransactionPropagation.RequiresNew || Transaction.Current == null)
        {
            var scopeOption = propagation == TransactionPropagation.RequiresNew
                ? TransactionScopeOption.RequiresNew
                : TransactionScopeOption.Required;

            var commandContext = CommandContextStack.Current;
            var transactionScope = new TransactionScope(scopeOption, TransactionScopeAsyncFlowOption.Enabled);

            if (commandContext != null)
            {
                commandContext.AddCloseListener(new TransactionCommandContextCloseListener(transactionScope));
                return next(command);
            }

            try
            {
                var result = next(command);
                transactionScope.Complete();
                return result;
            }
            finally
            {
                transactionScope.Dispose();
            }
        }

        return next(command);
    }

    public override async Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        var propagation = CommandConfigStack.Current?.TransactionPropagation ?? TransactionPropagation.Required;

        if (propagation == TransactionPropagation.NotSupported)
        {
            return await next(command);
        }

        if (propagation == TransactionPropagation.RequiresNew || Transaction.Current == null)
        {
            var scopeOption = propagation == TransactionPropagation.RequiresNew
                ? TransactionScopeOption.RequiresNew
                : TransactionScopeOption.Required;

            var transactionScope = new TransactionScope(scopeOption, TransactionScopeAsyncFlowOption.Enabled);

            var commandContext = CommandContextStack.Current;
            if (commandContext != null)
            {
                commandContext.AddCloseListener(new TransactionCommandContextCloseListener(transactionScope));
                return await next(command);
            }

            try
            {
                var result = await next(command);
                transactionScope.Complete();
                return result;
            }
            finally
            {
                transactionScope.Dispose();
            }
        }

        return await next(command);
    }
}

