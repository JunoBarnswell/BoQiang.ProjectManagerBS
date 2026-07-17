using AsterERP.Workflow.Core.Command;
using AsterERP.Workflow.Core.Integration;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class WorkflowIntegrationContextAsyncTests
{
    [Fact]
    public async Task IntegrationContextOperations_UseAsyncExecutorAndPreserveCancellation()
    {
        var executor = new CapturingCommandExecutor();
        var service = new IntegrationContextServiceImpl(executor);
        var entity = new IntegrationContextEntity { Id = "integration-1" };
        using var cancellation = new CancellationTokenSource();

        await service.FindByIdAsync(entity.Id, cancellation.Token);
        Assert.Equal(cancellation.Token, executor.CancellationToken);

        await service.AddIntegrationContextAsync(entity, cancellation.Token);
        Assert.Equal(cancellation.Token, executor.CancellationToken);

        await service.UpdateIntegrationContextAsync(entity, cancellation.Token);
        Assert.Equal(cancellation.Token, executor.CancellationToken);

        await service.DeleteIntegrationContextAsync(entity, cancellation.Token);
        Assert.Equal(cancellation.Token, executor.CancellationToken);
    }

    [Fact]
    public void IntegrationContextService_ExposesOnlyAsyncOperations()
    {
        var synchronousMethods = typeof(IIntegrationContextService)
            .GetMethods()
            .Where(method => !typeof(Task).IsAssignableFrom(method.ReturnType));

        Assert.Empty(synchronousMethods);
    }

    private sealed class CapturingCommandExecutor : ICommandExecutor
    {
        public object? LastCommand { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public ICommandInterceptor First { get; } = new CapturingCommandInterceptor();

        public T Execute<T>(ICommand<T> command)
        {
            LastCommand = command;
            return default!;
        }

        public Task<T> ExecuteAsync<T>(ICommand<T> command, CancellationToken cancellationToken = default)
        {
            LastCommand = command;
            CancellationToken = cancellationToken;
            return Task.FromResult(default(T)!);
        }
    }

    private sealed class CapturingCommandInterceptor : ICommandInterceptor
    {
        public ICommandInterceptor? Next { get; set; }

        public T Execute<T>(ICommand<T> command, Func<ICommand<T>, T> next) => next(command);

        public Task<T> ExecuteAsync<T>(
            ICommand<T> command,
            Func<ICommand<T>, Task<T>> next,
            CancellationToken cancellationToken = default) => next(command);
    }
}
