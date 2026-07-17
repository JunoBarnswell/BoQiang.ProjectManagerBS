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

public class DebugCommandInvoker : InterceptorCommandInvoker
{
    private readonly ILogger<DebugCommandInvoker> _logger;

    public DebugCommandInvoker()
    {
        _logger = NullLogger<DebugCommandInvoker>.Instance;
    }

    public DebugCommandInvoker(ILogger<DebugCommandInvoker> logger)
    {
        _logger = logger;
    }

    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        _logger.LogDebug("Executing command: {CommandType}", command.GetType().Name);
        _logger.LogDebug("CommandContext available: {HasContext}", CommandContextStack.HasContext);

        return base.Execute(command, next);
    }

    public override async Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Executing command async: {CommandType}", command.GetType().Name);
        _logger.LogDebug("CommandContext available: {HasContext}", CommandContextStack.HasContext);

        return await base.ExecuteAsync(command, next, cancellationToken);
    }
}

