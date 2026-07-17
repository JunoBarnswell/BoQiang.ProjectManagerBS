using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using AsterERP.Workflow.Core.Engine;
using AsterERP.Workflow.Core.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AsterERP.Workflow.Core.Command;

public class CommandContextInterceptor : AbstractCommandInterceptor
{
    private readonly CommandContextFactory _commandContextFactory;
    private readonly IProcessEngineConfiguration _processEngineConfiguration;
    private readonly ILogger<CommandContextInterceptor> _logger;

    public CommandContextInterceptor()
    {
        _commandContextFactory = new CommandContextFactory();
        _processEngineConfiguration = null!;
        _logger = NullLogger<CommandContextInterceptor>.Instance;
    }

    public CommandContextInterceptor(
        CommandContextFactory commandContextFactory,
        IProcessEngineConfiguration processEngineConfiguration)
    {
        _commandContextFactory = commandContextFactory;
        _processEngineConfiguration = processEngineConfiguration;
        _logger = NullLogger<CommandContextInterceptor>.Instance;
    }

    public CommandContextInterceptor(
        CommandContextFactory commandContextFactory,
        IProcessEngineConfiguration processEngineConfiguration,
        ILogger<CommandContextInterceptor> logger)
    {
        _commandContextFactory = commandContextFactory;
        _processEngineConfiguration = processEngineConfiguration;
        _logger = logger;
    }

    public CommandContextFactory CommandContextFactory => _commandContextFactory;
    public IProcessEngineConfiguration ProcessEngineConfiguration => _processEngineConfiguration;

    public override T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        throw new NotSupportedException("Synchronous command execution is not supported. Use ExecuteAsync.");
    }

    public override async Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        var context = CommandContextStack.Current;
        bool contextReused = false;
        bool commandSucceeded = false;
        var config = CommandConfigStack.Current;
        var contextReusePossible = config?.ContextReusePossible ?? true;
        IServiceScope? commandScope = null;
        IDisposable? serviceProviderScope = null;

        try
        {
            if (!CommandContextStack.HasContext || context == null || !contextReusePossible)
            {
                var ambientServiceProvider = ProcessEngineServiceProviderAccessor.Current;
                if (ambientServiceProvider is null)
                {
                    commandScope = CreateCommandScope();
                    serviceProviderScope = ProcessEngineServiceProviderAccessor.Push(commandScope?.ServiceProvider);
                }
                else
                {
                    serviceProviderScope = ProcessEngineServiceProviderAccessor.Push(ambientServiceProvider);
                }

                context = _commandContextFactory.CreateCommandContext(command);
            }
            else
            {
                _logger.LogDebug("Valid context found. Reusing it for the current command '{CommandType}'", command.GetType().Name);
                contextReused = true;
            }
        }
        catch
        {
            serviceProviderScope?.Dispose();
            commandScope?.Dispose();
            throw;
        }

        try
        {
            await context!.InitializeAsync(cancellationToken);
            CommandContextStack.Push(context);
            var result = await next(command);
            commandSucceeded = true;
            return result;
        }
        catch
        {
            if (!contextReused && !context!.IsClosed)
            {
                await context.RollbackAsync();
            }

            throw;
        }
        finally
        {
            CommandContextStack.Pop();
            if (!contextReused)
            {
                if (!context!.IsClosed && commandSucceeded)
                {
                    await context.CommitAsync();
                }

                await context.CloseAsync(cancellationToken);
            }

            serviceProviderScope?.Dispose();
            commandScope?.Dispose();
        }
    }

    private IServiceScope? CreateCommandScope()
    {
        return _processEngineConfiguration.ServiceProvider
            ?.GetService<IServiceScopeFactory>()
            ?.CreateScope();
    }
}

