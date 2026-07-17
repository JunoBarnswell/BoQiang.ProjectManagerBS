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

public class LogInterceptor : AbstractCommandInterceptor
{
    private readonly ILogger<LogInterceptor> _logger;

    public LogInterceptor(ILogger<LogInterceptor> logger)
    {
        _logger = logger;
    }

    public LogInterceptor()
    {
        _logger = NullLogger<LogInterceptor>.Instance;
    }

    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return next(command);
        }

        _logger.LogDebug("--- starting {CommandName} ---", command.GetType().Name);
        try
        {
            return next(command);
        }
        finally
        {
            _logger.LogDebug("--- {CommandName} finished ---", command.GetType().Name);
        }
    }

    public override async Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        if (!_logger.IsEnabled(LogLevel.Debug))
        {
            return await next(command);
        }

        _logger.LogDebug("--- starting {CommandName} ---", command.GetType().Name);
        try
        {
            return await next(command);
        }
        finally
        {
            _logger.LogDebug("--- {CommandName} finished ---", command.GetType().Name);
        }
    }
}

