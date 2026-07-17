using System;
using System.Collections.Generic;
using System.Threading;
using AsterERP.Workflow.Core.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace AsterERP.Workflow.Core.Context;

public static class ProcessEngineServiceProviderAccessor
{
    private static readonly AsyncLocal<IServiceProvider?> CurrentProvider = new();

    public static IServiceProvider? Current => CurrentProvider.Value;

    public static IDisposable Push(IServiceProvider? serviceProvider)
    {
        var previous = CurrentProvider.Value;
        CurrentProvider.Value = serviceProvider;
        return new RestoreScope(previous);
    }

    public static IServiceProvider? Resolve(IProcessEngineConfiguration? configuration)
    {
        return CurrentProvider.Value;
    }

    public static T? GetService<T>(IProcessEngineConfiguration? configuration) where T : class
    {
        return Resolve(configuration)?.GetService<T>();
    }

    public static object? GetService(IProcessEngineConfiguration? configuration, Type serviceType)
    {
        return Resolve(configuration)?.GetService(serviceType);
    }

    public static T GetRequiredService<T>(IProcessEngineConfiguration? configuration) where T : notnull
    {
        var serviceProvider = Resolve(configuration)
            ?? throw new InvalidOperationException(
                $"{typeof(T).Name} requires an active process engine command scope.");
        return serviceProvider.GetRequiredService<T>();
    }

    public static IEnumerable<T> GetServices<T>(IProcessEngineConfiguration? configuration)
    {
        return Resolve(configuration)?.GetServices<T>() ?? Array.Empty<T>();
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly IServiceProvider? _previous;
        private bool _disposed;

        public RestoreScope(IServiceProvider? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentProvider.Value = _previous;
            _disposed = true;
        }
    }
}
