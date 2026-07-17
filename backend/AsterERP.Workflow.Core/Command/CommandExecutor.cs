using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AsterERP.Workflow.Common;

namespace AsterERP.Workflow.Core.Command;

public interface ICommandExecutor
{
    T Execute<T>(ICommand<T> command);
    Task<T> ExecuteAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default);
    ICommandInterceptor First { get; }
}

public interface ICommandInterceptor
{
    T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next);
    Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default);
    ICommandInterceptor? Next { get; set; }
}

public abstract class AbstractCommandInterceptor : ICommandInterceptor
{
    public ICommandInterceptor? Next { get; set; }

    public abstract T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next);
    public abstract Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default);
}

public class CommandInvoker : ICommandInterceptor
{
    public ICommandInterceptor? Next { get; set; }

    public T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        return command.Execute(GetCommandContext());
    }

    public async Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        return await command.ExecuteAsync(GetCommandContext(), cancellationToken);
    }

    protected virtual ICommandContext GetCommandContext()
    {
        return CommandContextStack.Current!;
    }
}

public class CommandExecutorImplementation : ICommandExecutor
{
    private ICommandContext? _context;
    private ICommandInterceptor _first = null!;

    public CommandExecutorImplementation()
    {
        _context = null;
        var invoker = new CommandInvoker();
        _first = invoker;
    }

    public CommandExecutorImplementation(IEnumerable<ICommandInterceptor> interceptors)
        : this(null, interceptors)
    {
    }

    public CommandExecutorImplementation(ICommandContext? context, IEnumerable<ICommandInterceptor> interceptors)
    {
        _context = context;
        var contextInvoker = new CommandInvoker();
        ICommandInterceptor? current = null;
        foreach (var interceptor in interceptors)
        {
            interceptor.Next = null;
            if (current == null)
            {
                _first = interceptor;
                current = interceptor;
            }
            else
            {
                current.Next = interceptor;
                current = interceptor;
            }
        }
        if (current == null)
        {
            _first = contextInvoker;
        }
        else
        {
            current.Next = contextInvoker;
        }
    }

    public T Execute<T>(ICommand<T> command)
    {
        return ExecuteInternal(command, First);
    }

    public async Task<T> ExecuteAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default)
    {
        return await ExecuteInternalAsync(command, First, cancellationToken);
    }

    public ICommandInterceptor First => _first;

    private T ExecuteInternal<T>(ICommand<T> command, ICommandInterceptor interceptor)
    {
        ICommandContext? currentContext = _context ?? CommandContextStack.Current;

        if (interceptor.Next == null)
        {
            return command.Execute(currentContext!);
        }
        return interceptor.Execute(command, cmd => ExecuteInternal(cmd, interceptor.Next!));
    }

    private async Task<T> ExecuteInternalAsync<T>(ICommand<T> command, ICommandInterceptor interceptor, CancellationToken cancellationToken)
    {
        ICommandContext? currentContext = _context ?? CommandContextStack.Current;

        if (interceptor.Next == null)
        {
            return await command.ExecuteAsync(currentContext!, cancellationToken);
        }
        return await interceptor.ExecuteAsync(command,
            async cmd => await ExecuteInternalAsync(cmd, interceptor.Next!, cancellationToken),
            cancellationToken);
    }
}

public class ContextCommandInvoker : ICommandInterceptor
{
    private readonly ICommandContext _context;

    public ContextCommandInvoker(ICommandContext context)
    {
        _context = context;
    }

    public ICommandInterceptor? Next { get; set; }

    public T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next)
    {
        return command.Execute(_context);
    }

    public Task<T> ExecuteAsync<T>(ICommand<T> command, Func<ICommand<T>, Task<T>> next, CancellationToken cancellationToken = default)
    {
        return command.ExecuteAsync(_context, cancellationToken);
    }
}
