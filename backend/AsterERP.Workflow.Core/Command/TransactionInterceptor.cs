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

public abstract class TransactionInterceptor : AbstractCommandInterceptor
{
    protected readonly IProcessEngineConfiguration ProcessEngineConfiguration;

    protected TransactionInterceptor(IProcessEngineConfiguration processEngineConfiguration)
    {
        ProcessEngineConfiguration = processEngineConfiguration;
    }

    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        var config = CommandConfigStack.Current ?? new CommandConfig();

        switch (config.TransactionPropagation)
        {
            case TransactionPropagation.Required:
                return ExecuteRequired(command, next);
            case TransactionPropagation.RequiresNew:
                return ExecuteRequiresNew(command, next);
            case TransactionPropagation.NotSupported:
                return next(command);
            default:
                return next(command);
        }
    }

    public override async Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        var config = CommandConfigStack.Current ?? new CommandConfig();

        switch (config.TransactionPropagation)
        {
            case TransactionPropagation.Required:
                return await ExecuteRequiredAsync(command, next, cancellationToken);
            case TransactionPropagation.RequiresNew:
                return await ExecuteRequiresNewAsync(command, next, cancellationToken);
            case TransactionPropagation.NotSupported:
                return await next(command);
            default:
                return await next(command);
        }
    }

    protected abstract T ExecuteRequired<T>(ICommand<T> command, Func<ICommand<T>, T> next);
    protected abstract T ExecuteRequiresNew<T>(ICommand<T> command, Func<ICommand<T>, T> next);
    protected abstract Task<T> ExecuteRequiredAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken);
    protected abstract Task<T> ExecuteRequiresNewAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken);
}

